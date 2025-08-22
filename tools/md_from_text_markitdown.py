#!/usr/bin/env python3
import sys,re,unicodedata

def generate(txt: str) -> str:
    lines = txt.replace('\r\n','\n').split('\n')
    out = []
    for l in lines:
        line = l.rstrip()
        if re.fullmatch(r'[A-Z0-9 ]{3,}', line):
            level = 1 if len(line.split()) <= 3 else 2
            out.append('#'*level + ' ' + line)
        else:
            out.append(line)
    return '\n'.join(out).rstrip() + '\n'

if __name__ == '__main__':
    with open(sys.argv[1], 'r', encoding='utf-8') as f:
        txt = f.read()
    md = generate(txt)
    sys.stdout.write(md)
