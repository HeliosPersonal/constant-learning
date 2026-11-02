import json
import math
import re
from datetime import datetime, timedelta, timezone
from pathlib import Path

root = Path(__file__).parent / 'results' / 'final'
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
(root / 'summary-analysis.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(summary, ensure_ascii=False, indent=2))
