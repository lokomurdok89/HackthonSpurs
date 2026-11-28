"""Synthetic workbook and multipart encoders shared by HTTP tests."""
import io
import uuid
import zipfile


def workbook():
    """Build the minimal ZIP parts needed to recognize a synthetic XLSX."""
    data = io.BytesIO()
    with zipfile.ZipFile(data, "w") as book:
        book.writestr("[Content_Types].xml", '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/></Types>')
        book.writestr("xl/workbook.xml", '<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"/>')
    return data.getvalue()


def form(parts):
    """Encode parts (field, filename, media type, bytes) and return the body and multipart content type."""
    boundary = uuid.uuid4().hex
    data = bytearray()
    for field, name, mime, content in parts:
        data.extend(f'--{boundary}\r\nContent-Disposition: form-data; name="{field}"; filename="{name}"\r\nContent-Type: {mime}\r\n\r\n'.encode())
        data.extend(content + b"\r\n")
    data.extend(f"--{boundary}--\r\n".encode())
    return bytes(data), "multipart/form-data; boundary=" + boundary
