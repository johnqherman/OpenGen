#!/bin/bash
curl -s --max-time 5 "https://api.cs2inspects.com/getGenCode?url=$1" \
  | python3 -c "import json,sys; d=json.load(sys.stdin); print(json.dumps(d.get('genCodeDetail', {})))"
