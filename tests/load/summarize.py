import gzip
import json
import math
import os
import re
import sys
from datetime import datetime, timedelta, timezone
from pathlib import Path

root = Path(os.environ.get('LOAD_RESULTS_DIR', Path(__file__).parent / 'results' / 'final'))
text = (root / 'metrics-after.prom').read_text(encoding='utf-8-sig')

def histogram(name, budget):
    buckets = [(float(bound), int(count)) for bound, count in re.findall(rf'^{name}_bucket\{{le="([0-9.]+)"\}} (\d+)$', text, re.M)]
    count = int(re.search(rf'^{name}_count (\d+)$', text, re.M).group(1))
    upper = next((b for b, n in buckets if n >= math.ceil(count * .95)), None) if count else None
    lower = max((b for b, n in buckets if n < math.ceil(count * .95)), default=0) if count else None
    good = dict(buckets).get(float(budget), 0)
    return {'count': count, 'within_budget': good, 'budget_ms': budget, 'p95_interval_ms': [lower, upper], 'fraction_within': good / count if count else None, 'wilson_95': wilson(good, count)}

def wilson(success, count):
    if not count: return None
    z = 1.959963984540054
    p = success / count
    center = (p + z*z/(2*count))/(1+z*z/count)
    radius = z*math.sqrt(p*(1-p)/count + z*z/(4*count*count))/(1+z*z/count)
    return [center-radius, center+radius]

samples = json.loads((root / 'browser-samples.json').read_text(encoding='utf-8-sig'))
values = sorted(s['value_ms'] for s in samples if 'value_ms' in s)
due = datetime.fromisoformat((root / 'reminder-due.txt').read_text().strip().replace('Z', '+00:00'))
sends = []
send_file = root / 'reminder-sends.csv'
if send_file.exists():
    for row in send_file.read_text().splitlines():
        chat, stamp = row.split(',', 1)
        sends.append((chat, datetime.fromisoformat(stamp.replace('Z', '+00:00'))))
within = sum(due <= at <= due + timedelta(seconds=120) for _, at in sends)
summary = {
    'answer': histogram('trainer_answer_duration_ms', 500),
    'search': histogram('trainer_dictionary_search_duration_ms', 300),
    'browser': {'opens': len(samples), 'successful': len(values), 'within_1500': sum(v <= 1500 for v in values), 'p95_nearest_rank_ms': values[math.ceil(.95*len(values))-1] if values else None, 'wilson_95_within_1500': wilson(sum(v <= 1500 for v in values), len(values))},
    'reminders': {'due': due.isoformat(), 'calls': len(sends), 'distinct_recipients': len({chat for chat, _ in sends}), 'within_120s': within, 'expected': 1000, 'wilson_95_expected': wilson(within, 1000)},
}
before = (root / 'metrics-before.prom').read_text(encoding='utf-8-sig')
initial = {name: int(re.search(rf'^{name}_count (\d+)$', before, re.M).group(1)) for name in ('trainer_answer_duration_ms', 'trainer_dictionary_search_duration_ms', 'trainer_today_ready_ms')}
k6 = json.loads((root / 'k6-summary.json').read_text(encoding='utf-8-sig'))
checks = k6['root_group']['checks']
reconciliation = json.loads((root / 'reconciliation.json').read_text(encoding='utf-8-sig'))
metrics = k6['metrics']
checks_ok = bool(checks) and all(c['fails'] == 0 for c in checks.values())
journey_ok = metrics.get('journey_failures', {}).get('count', -1) == 0
raw_path = root / 'k6-raw.json'
raw_stream = raw_path.open(encoding='utf-8') if raw_path.exists() else gzip.open(root / 'k6-raw.json.gz', 'rt', encoding='utf-8')
with raw_stream as raw:
    for line in raw:
        record = json.loads(line)
        if record.get('type') == 'Point':
            start = datetime.fromisoformat(record['data']['time'][:26].rstrip('.') + '+00:00')
            break
log = (root / 'k6-errors.log').read_text(encoding='utf-8-sig').splitlines()
unexpected = []
tail = []
for line in log:
    match = re.search(r'time="([^"]+)"', line)
    if not match:
        unexpected.append(line)
        continue
    timestamp = datetime.fromisoformat(match.group(1).replace('Z', '+00:00'))
    offset = (timestamp - start).total_seconds()
    if 928 <= offset <= 932 and ('HTTP 0 ' in line or 'Хибна оцінка: undefined' in line):
        tail.append(line)
    else:
        unexpected.append(line)
summary['log_classification'] = {'start_utc': start.isoformat(), 'grace_end_cancellation_lines': len(tail), 'unexpected_lines': len(unexpected), 'unexpected': unexpected}
browser = summary['browser']
reminder = summary['reminders']
answer_count = summary['answer']['count']
summary['gates'] = {
    'zero_baseline': all(value == 0 for value in initial.values()),
    '100_vus': metrics['vus']['min'] == 100 and metrics['vus']['max'] == 100,
    'journey_checks': checks_ok and journey_ok and metrics['http_req_failed']['value'] == 0 and not unexpected,
    'duration': metrics['http_reqs']['count'] / metrics['http_reqs']['rate'] >= 900,
    'server_answer': answer_count > 0 and summary['answer']['fraction_within'] >= .95,
    'server_search': summary['search']['count'] > 0 and summary['search']['fraction_within'] >= .95,
    'browser': browser['opens'] == 50 and browser['successful'] == 50 and browser['p95_nearest_rank_ms'] <= 1500,
    'reminders': reminder['calls'] == 1000 and reminder['distinct_recipients'] == 1000 and reminder['within_120s'] >= 990,
    'reconciliation': reconciliation['events'] == answer_count == metrics.get('graded_answers', {}).get('count', -1) == reconciliation['distinct_items'] == reconciliation['graded_items'] and all(reconciliation[key] == 0 for key in ('unlinked', 'duplicates', 'missing_event', 'missing_item')),
}
summary['initial_counts'] = initial
summary['reconciliation'] = reconciliation
summary['passed'] = all(summary['gates'].values())
(root / 'summary-analysis.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(summary, ensure_ascii=False, indent=2))
sys.exit(0 if summary['passed'] else 1)
