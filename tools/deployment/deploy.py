"""Production-деплой перевіреного main; викликається лише захищеним Actions job."""

import base64
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
import time
import urllib.request


NAMESPACE = "apps-production"
APP = "constant-learning"
ANNOTATION = "constant-learning.github.io/"
ROOT = Path(__file__).resolve().parents[2]


class DeploymentError(Exception):
    pass


def required(name):
    value = os.environ.get(name, "")
    if not value.strip():
        raise DeploymentError(f"Не налаштовано {name}.")
    return value


def command(args, timeout=60):
    try:
        result = subprocess.run(args, capture_output=True, text=True, timeout=timeout)
    except (OSError, subprocess.TimeoutExpired):
        raise DeploymentError(f"Не вдалося виконати {args[0]} або вичерпано timeout.") from None
    if result.returncode:
        # stderr kubectl може містити маніфест або дані API; не публікуємо його.
        operation = args[3] if args[1] == "--namespace" else args[1]
        raise DeploymentError(f"Команда {args[0]} {operation} завершилася помилкою.")
    return result.stdout


def kubectl(*args, timeout=60):
    return command(["kubectl", "--namespace", NAMESPACE, *args], timeout)


def github_main():
    repository = required("GITHUB_REPOSITORY")
    request = urllib.request.Request(
        f"https://api.github.com/repos/{repository}/git/ref/heads/main",
        headers={"Authorization": f"Bearer {required('GH_TOKEN')}",
                 "Accept": "application/vnd.github+json"},
    )
    try:
        with urllib.request.urlopen(request, timeout=20) as response:
            return json.load(response)["object"]["sha"]
    except Exception:
        raise DeploymentError("Не вдалося перевірити поточний SHA main через GitHub API.") from None


def is_current(sha, run_number, previous):
    deployed_run = previous.get("metadata", {}).get("annotations", {}).get(ANNOTATION + "run-number", "0")
    try:
        return github_main() == sha and int(deployed_run) <= run_number
    except ValueError:
        raise DeploymentError("Некоректний номер попереднього деплою; потрібна перевірка оператора.") from None


def settings():
    if required("GITHUB_EVENT_NAME") != "push" or required("GITHUB_REF") != "refs/heads/main":
        raise DeploymentError("Деплой дозволено лише для push main.")
    sha = required("GITHUB_SHA")
    digest = required("DEPLOY_DIGEST")
    image = required("DEPLOY_IMAGE")
    host = required("PUBLIC_HOST")
    if not re.fullmatch(r"[0-9a-f]{40}", sha) or not re.fullmatch(r"sha256:[0-9a-f]{64}", digest):
        raise DeploymentError("Некоректний SHA коміту або digest образу.")
    expected_image = "ghcr.io/" + required("GITHUB_REPOSITORY").lower()
    if image != expected_image:
        raise DeploymentError("Образ не належить цьому репозиторію.")
    if len(host) > 253 or not re.fullmatch(r"[a-z0-9](?:[a-z0-9.-]*[a-z0-9])?", host) or "." not in host:
        raise DeploymentError("PUBLIC_HOST має бути DNS-іменем без схеми, порту або шляху.")
    return sha, int(required("GITHUB_RUN_NUMBER")), image + "@" + digest, host


def secret(name, values, kind="Opaque"):
    return {"apiVersion": "v1", "kind": "Secret", "metadata": {"name": name, "namespace": NAMESPACE},
            "type": kind, "data": {key: base64.b64encode(value.encode()).decode() for key, value in values.items()}}


def prepare(directory, image, host, sha, run_number):
    encoded = os.environ.get("KUBECONFIG_BASE64", "").strip()
    if encoded:
        config = directory / "kubeconfig"
        try:
            config.write_bytes(base64.b64decode("".join(encoded.split()), validate=True))
        except ValueError:
            raise DeploymentError("KUBECONFIG має містити валідний base64.") from None
        config.chmod(0o600)
    else:
        config = Path.home() / ".kube/config"
        if not config.is_file():
            raise DeploymentError("Немає KUBECONFIG secret або ~/.kube/config на runner Helios.")
    os.environ["KUBECONFIG"] = str(config)

    secrets = [
        secret(APP + "-infisical", {name: required(name) for name in
               ["INFISICAL_CLIENT_ID", "INFISICAL_CLIENT_SECRET", "INFISICAL_PROJECT_ID"]}),
    ]
    secret_file = directory / "secrets.json"
    secret_file.write_text(json.dumps({"apiVersion": "v1", "kind": "List", "items": secrets}), encoding="utf-8")
    secret_file.chmod(0o600)

    shutil.copytree(ROOT / "k8s", directory / "k8s")
    deployment = directory / "k8s/base/deployment.yaml"
    deployment.write_text(deployment.read_text(encoding="utf-8").replace("PUBLIC_HOST_REPLACED_BY_CICD", host), encoding="utf-8")
    overlay = directory / "k8s/overlays/production"
    ingress = overlay / "ingress.yaml"
    ingress.write_text(ingress.read_text(encoding="utf-8").replace("PUBLIC_HOST_REPLACED_BY_CICD", host), encoding="utf-8")
    kustomization = overlay / "kustomization.yaml"
    with kustomization.open("a", encoding="utf-8") as stream:
        stream.write(f"\nimages:\n  - name: ghcr.io/GITHUB_USERNAME/constant-learning\n"
                     f"    newName: {image.split('@')[0]}\n    digest: {image.split('@')[1]}\n"
                     f"commonAnnotations:\n  {ANNOTATION}commit: '{sha}'\n"
                     f"  {ANNOTATION}run-number: '{run_number}'\n")
    rendered = command(["kubectl", "kustomize", str(overlay)])
    if "REPLACED_BY" in rendered or "GITHUB_USERNAME" in rendered or image not in rendered:
        raise DeploymentError("У маніфестах залишилися заповнювачі або відсутній digest.")
    manifests = directory / "manifests.yaml"
    manifests.write_text(rendered, encoding="utf-8")
    return secret_file, manifests


def health(host, attempts=12):
    for attempt in range(attempts):
        try:
            request = urllib.request.Request(f"https://{host}/api/telegram/health", headers={"Cache-Control": "no-cache"})
            with urllib.request.urlopen(request, timeout=10) as response:
                if (response.status == 200 and response.geturl() == request.full_url
                        and json.load(response).get("status") == "healthy"):
                    return
        except Exception:
            pass
        if attempt + 1 < attempts:
            time.sleep(5)
    raise DeploymentError("Зовнішня HTTPS-перевірка здоров’я не пройшла.")


def diagnostics():
    # Лише стан і стандартні причини: без env, Secret, журналів та повідомлень API.
    try:
        deployment = json.loads(kubectl("get", "deployment", APP, "-o", "json"))
        status = deployment.get("status", {})
        print(json.dumps({"deployment": APP, "updated": status.get("updatedReplicas", 0),
                          "available": status.get("availableReplicas", 0),
                          "conditions": [{key: condition.get(key) for key in ["type", "status", "reason"]}
                                         for condition in status.get("conditions", [])]}, ensure_ascii=False))
        pods = json.loads(kubectl("get", "pods", "-l", "app=" + APP, "-o", "json"))
        for pod in pods["items"]:
            states = []
            for container in pod.get("status", {}).get("containerStatuses", []):
                state = container.get("state", {})
                states.append({"ready": container["ready"], "restarts": container["restartCount"],
                               "state": list(state), "reason": next(iter(state.values()), {}).get("reason")})
            print(json.dumps({"pod": pod["metadata"]["name"], "phase": pod.get("status", {}).get("phase"),
                              "containers": states}, ensure_ascii=False))
    except Exception:
        print("Діагностика Pod недоступна; перевірте доступ Kubernetes.")


def deploy():
    sha, run_number, image, host = settings()
    if github_main() != sha:
        print("Пропущено застарілий SHA: main уже змінився.")
        return
    with tempfile.TemporaryDirectory(prefix="constant-learning-", dir=os.environ.get("RUNNER_TEMP")) as temporary:
        directory = Path(temporary)
        secret_file, manifests = prepare(directory, image, host, sha, run_number)
        kubectl("get", "namespace", NAMESPACE)
        kubectl("get", "secret", "cloudflare-origin", "-o", "name")
        previous_json = kubectl("get", "deployment", APP, "--ignore-not-found", "-o", "json")
        previous = json.loads(previous_json) if previous_json.strip() else {}
        previous_images = [c["image"] for c in previous.get("spec", {}).get("template", {}).get("spec", {}).get("containers", [])]
        # Повторна перевірка під concurrency lock безпосередньо перед першою зміною кластера.
        if not is_current(sha, run_number, previous):
            print("Пропущено застарілий запуск; новіший реліз не змінено.")
            return
        summary = f"### Production\n\nКоміт: `{sha}`\n\nПопередні образи: `{', '.join(previous_images) or 'відсутні'}`\n\nНовий образ: `{image}`\n"
        if os.environ.get("GITHUB_STEP_SUMMARY"):
            with open(os.environ["GITHUB_STEP_SUMMARY"], "a", encoding="utf-8") as stream:
                stream.write(summary)
        try:
            print("Застосування прикладних секретів.")
            kubectl("apply", "--server-side", "--field-manager=constant-learning-deploy", "-f", str(secret_file))
            print("Застосування Deployment, Service та Ingress.")
            kubectl("apply", "-f", str(manifests))
            print("Очікування rollout (8 хвилин).")
            kubectl("rollout", "status", "deployment/" + APP, "--timeout=8m", timeout=500)
            actual = json.loads(kubectl("get", "deployment", APP, "-o", "json"))
            containers = actual["spec"]["template"]["spec"]["containers"]
            if next(c["image"] for c in containers if c["name"] == APP) != image:
                raise DeploymentError("Образ Deployment змінився під час перевірки.")
            print("Перевірка публічного HTTPS health endpoint.")
            health(host)
            print(f"Production готовий: {sha}, {image}")
        except Exception:
            diagnostics()
            raise


if __name__ == "__main__":
    os.umask(0o077)
    try:
        deploy()
    except DeploymentError as error:
        print(f"::error::{error}")
        sys.exit(1)
    except Exception:
        # Значення винятків сторонніх бібліотек можуть містити секрети.
        print("::error::Деплой не завершено. Перевірте останній етап, стан Pod, налаштування та захищені журнали Helios.")
        sys.exit(1)
