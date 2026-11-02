"""Локальні перевірки деплою без доступу до кластера або GitHub."""

import base64
import contextlib
import io
import json
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import MagicMock, patch

import deploy


ENV = {
    "GITHUB_EVENT_NAME": "push", "GITHUB_REF": "refs/heads/main",
    "GITHUB_SHA": "a" * 40, "GITHUB_RUN_NUMBER": "20",
    "GITHUB_REPOSITORY": "ViacheslavMelnichenko/constant-learning", "GH_TOKEN": "test-only",
    "DEPLOY_IMAGE": "ghcr.io/viacheslavmelnichenko/constant-learning",
    "DEPLOY_DIGEST": "sha256:" + "b" * 64, "PUBLIC_HOST": "test.example.org",
    "KUBECONFIG_BASE64": base64.b64encode(b"apiVersion: v1\nkind: Config\n").decode(),
    "CONNECTIONSTRINGS__DEFAULTCONNECTION": "Host=test-only;Password=test-secret",
    "TELEGRAM_BOT_TOKEN": "test-secret", "TELEGRAM_SECRET_TOKEN": "test-only",
    "GHCR_PULL_USERNAME": "test-only", "GHCR_PULL_TOKEN": "test-secret",
}


class DeploymentTests(unittest.TestCase):
    def setUp(self):
        self.environment = patch.dict(os.environ, ENV)
        self.environment.start()
        self.addCleanup(self.environment.stop)

    def test_pr_cannot_deploy(self):
        os.environ["GITHUB_EVENT_NAME"] = "pull_request"
        with self.assertRaises(deploy.DeploymentError), patch.object(deploy, "kubectl") as kubectl:
            deploy.deploy()
        kubectl.assert_not_called()

    def test_mutable_image_and_invalid_host_are_rejected(self):
        for key, value in [("DEPLOY_DIGEST", "latest"), ("PUBLIC_HOST", "https://example.org/path"),
                           ("DEPLOY_IMAGE", "ghcr.io/another/app"), ("TELEGRAM_SECRET_TOKEN", "bad token")]:
            with self.subTest(key=key), patch.dict(os.environ, {key: value}):
                with self.assertRaises(deploy.DeploymentError):
                    deploy.settings()

    def test_old_sha_never_touches_cluster(self):
        with patch.object(deploy, "github_main", return_value="c" * 40), patch.object(deploy, "kubectl") as kubectl:
            deploy.deploy()
        kubectl.assert_not_called()

    def test_old_run_cannot_replace_newer_release_even_after_force_push(self):
        previous = {"metadata": {"annotations": {deploy.ANNOTATION + "run-number": "21"}}}
        with patch.object(deploy, "github_main", return_value=ENV["GITHUB_SHA"]):
            self.assertFalse(deploy.is_current(ENV["GITHUB_SHA"], 20, previous))
            self.assertTrue(deploy.is_current(ENV["GITHUB_SHA"], 21, previous))

    def test_main_is_rechecked_before_first_mutation(self):
        with patch.object(deploy, "github_main", side_effect=[ENV["GITHUB_SHA"], "c" * 40]), \
                patch.object(deploy, "prepare", return_value=("secrets", "manifests")), \
                patch.object(deploy, "kubectl", return_value="") as kubectl:
            deploy.deploy()
        self.assertTrue(all(call.args[0] == "get" for call in kubectl.call_args_list))

    def test_rollout_failure_is_fatal_and_cleans_temporary_files(self):
        temporary = []

        def prepare(directory, *args):
            temporary.append(directory)
            (directory / "secret").write_text("test-secret")
            return directory / "secret", directory / "manifests"

        def kubectl(*args, **kwargs):
            if args[0] == "rollout":
                raise deploy.DeploymentError("rollout timeout")
            return ""

        with patch.object(deploy, "github_main", return_value=ENV["GITHUB_SHA"]), \
                patch.object(deploy, "prepare", side_effect=prepare), \
                patch.object(deploy, "kubectl", side_effect=kubectl), \
                patch.object(deploy, "diagnostics") as diagnostics, patch.object(deploy, "health") as health:
            with self.assertRaises(deploy.DeploymentError):
                deploy.deploy()
        diagnostics.assert_called_once()
        health.assert_not_called()
        self.assertFalse(temporary[0].exists())

    def test_health_failure_is_fatal(self):
        with patch.object(deploy.urllib.request, "urlopen", side_effect=OSError), patch.object(deploy.time, "sleep"):
            with self.assertRaises(deploy.DeploymentError):
                deploy.health("test.example.org", attempts=2)

    def test_health_requires_json_healthy_without_redirect(self):
        for body, url, succeeds in [
            (b'{"status":"healthy"}', "https://test.example.org/api/telegram/health", True),
            (b'{"status":"unhealthy"}', "https://test.example.org/api/telegram/health", False),
            (b'{"status":"healthy"}', "https://another.example.org/api/telegram/health", False),
        ]:
            response = MagicMock()
            response.status = 200
            response.geturl.return_value = url
            response.read.return_value = body
            response.__enter__.return_value = response
            with self.subTest(body=body, url=url), patch.object(deploy.urllib.request, "urlopen", return_value=response):
                if succeeds:
                    deploy.health("test.example.org", attempts=1)
                else:
                    with self.assertRaises(deploy.DeploymentError):
                        deploy.health("test.example.org", attempts=1)

    def test_success_waits_for_rollout_before_health(self):
        operations = []
        image = ENV["DEPLOY_IMAGE"] + "@" + ENV["DEPLOY_DIGEST"]

        def kubectl(*args, **kwargs):
            operations.append(args[0])
            if args[0] == "get" and args[1] == "deployment" and "--ignore-not-found" not in args:
                return json.dumps({"spec": {"template": {"spec": {"containers": [{"name": deploy.APP, "image": image}]}}}})
            return ""

        with patch.object(deploy, "github_main", return_value=ENV["GITHUB_SHA"]), \
                patch.object(deploy, "prepare", return_value=("secrets", "manifests")), \
                patch.object(deploy, "kubectl", side_effect=kubectl), \
                patch.object(deploy, "health", side_effect=lambda host: operations.append("health")):
            deploy.deploy()
        self.assertEqual(operations.count("apply"), 2)
        self.assertLess(operations.index("rollout"), operations.index("health"))

    def test_diagnostics_do_not_print_logs_env_or_messages(self):
        pod = {"metadata": {"name": "test-pod"}, "status": {"phase": "Pending", "containerStatuses": [
            {"ready": False, "restartCount": 1, "state": {"waiting": {"reason": "ImagePullBackOff", "message": "test-secret"}}}
        ]}}
        output = io.StringIO()
        with patch.object(deploy, "kubectl", return_value=json.dumps({"items": [pod]})), contextlib.redirect_stdout(output):
            deploy.diagnostics()
        self.assertIn("ImagePullBackOff", output.getvalue())
        self.assertNotIn("test-secret", output.getvalue())

    def test_real_kustomize_render_keeps_secrets_separate(self):
        sha, run, image, host = deploy.settings()
        with tempfile.TemporaryDirectory() as temporary:
            secrets, manifests = deploy.prepare(Path(temporary), image, host, sha, run)
            rendered = manifests.read_text(encoding="utf-8")
            self.assertIn(image, rendered)
            self.assertIn("namespace: apps-production", rendered)
            self.assertIn("host: test.example.org", rendered)
            self.assertIn("constant-learning-ghcr", rendered)
            self.assertNotIn("kind: Secret", rendered)
            self.assertNotIn("test-secret", rendered)
            self.assertNotIn("REPLACED_BY", rendered)
            objects = json.loads(secrets.read_text(encoding="utf-8"))["items"]
            telegram = next(obj for obj in objects if obj["metadata"]["name"].endswith("-telegram"))
            self.assertEqual(base64.b64decode(telegram["data"]["Telegram__MiniAppUrl"]).decode(), "https://test.example.org/")


if __name__ == "__main__":
    unittest.main()
