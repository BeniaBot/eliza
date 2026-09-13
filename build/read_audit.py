# Pulls the ranked prose out of a workflow's task output file.

import io
import json
import sys

path = sys.argv[1]
key = sys.argv[2] if len(sys.argv) > 2 else 'summary'

raw = io.open(path, encoding='utf-8', errors='replace').read()

start = raw.find('{')
end = raw.rfind('}')
if start < 0 or end < start:
    sys.exit('no JSON object in file')

try:
    data = json.loads(raw[start:end + 1])
except ValueError as e:
    sys.exit('could not parse: %s' % e)

value = data.get(key)
if value is None:
    sys.exit('keys present: %s' % ', '.join(sorted(data.keys())))

if isinstance(value, str):
    print(value)
else:
    print(json.dumps(value, ensure_ascii=False, indent=2))
