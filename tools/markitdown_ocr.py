import argparse
import time
from pathlib import Path

from PIL import Image
import pytesseract
from markitdown import MarkItDown
from markitdown._stream_info import StreamInfo
import io


def main():
    parser = argparse.ArgumentParser(
        description="Convert an image or text file to Markdown using pytesseract and markitdown"
    )
    parser.add_argument("input", help="Path to an image or text file")
    parser.add_argument("-o", "--out", help="Output Markdown file", required=True)
    parser.add_argument("--lang", default="ita", help="Tesseract language, e.g. 'eng' or 'ita'")
    args = parser.parse_args()

    inp = Path(args.input)
    md_file = Path(args.out)

    if inp.suffix.lower() == ".txt":
        text = inp.read_text(encoding="utf-8")
    else:
        img = Image.open(inp)
        t0 = time.time()
        text = pytesseract.image_to_string(img, lang=args.lang)
        t1 = time.time()
        print(f"OCR ms: {(t1 - t0) * 1000:.2f}")

    md = MarkItDown()
    stream = io.BytesIO(text.encode("utf-8"))
    t2 = time.time()
    result = md.convert(stream, stream_info=StreamInfo(mimetype="text/plain"))
    t3 = time.time()
    md_file.write_text(result.text_content, encoding="utf-8")
    print(f"Markdown ms: {(t3 - t2) * 1000:.2f}")


if __name__ == "__main__":
    main()
