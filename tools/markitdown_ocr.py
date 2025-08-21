import argparse
import time
from pathlib import Path

from PIL import Image
import pytesseract
from markitdown import MarkItDown


def main():
    parser = argparse.ArgumentParser(description="Convert image to Markdown using pytesseract and markitdown")
    parser.add_argument("image", help="Path to the image file")
    parser.add_argument("--lang", default="ita", help="Tesseract language, e.g. 'eng' or 'ita'")
    args = parser.parse_args()

    image_path = Path(args.image)
    img = Image.open(image_path)

    t0 = time.time()
    ocr_text = pytesseract.image_to_string(img, lang=args.lang)
    t1 = time.time()

    base = image_path.with_suffix("")
    ocr_file = base.with_name(base.name + "_pytesseract.txt")
    md_file = base.with_name(base.name + "_markitdown.md")
    ocr_file.write_text(ocr_text, encoding="utf-8")

    md = MarkItDown()
    t2 = time.time()
    result = md.convert(str(ocr_file))
    t3 = time.time()
    md_file.write_text(result.text_content, encoding="utf-8")

    print(f"OCR ms: {(t1 - t0) * 1000:.2f}")
    print(f"Markdown ms: {(t3 - t2) * 1000:.2f}")


if __name__ == "__main__":
    main()
