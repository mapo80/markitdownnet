import sys, json, time, statistics, platform, io
from markitdown import MarkItDown
from markitdown._stream_info import StreamInfo

if len(sys.argv) < 3:
    print("usage: run_markitdown_hot.py <input.txt> <output.md>", file=sys.stderr)
    sys.exit(1)

inp, outp = sys.argv[1], sys.argv[2]
with open(inp, 'r', encoding='utf-8') as f:
    text = f.read()

md = MarkItDown()
stream_info = StreamInfo(mimetype="text/plain")
# warm-up
md.convert(io.BytesIO(text.encode("utf-8")), stream_info=stream_info)

trials = []
last = ""
for _ in range(5):
    start = time.perf_counter()
    res = md.convert(io.BytesIO(text.encode("utf-8")), stream_info=stream_info)
    trials.append((time.perf_counter() - start) * 1000.0)
    last = res.text_content

with open(outp, 'w', encoding='utf-8') as f:
    f.write(last)

out = {
    "trials": trials,
    "avg": statistics.mean(trials),
    "stddev": statistics.pstdev(trials) if len(trials)>1 else 0.0,
    "env": {
        "python": platform.python_version(),
        "markitdown": getattr(__import__('markitdown'), '__version__', '')
    }
}
print(json.dumps(out))
