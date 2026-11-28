"""Small invented, printable multi-format documents for the private reader test."""
import io
import zipfile


def pdf(count):
    """Create count visibly numbered synthetic PDF pages without an external PDF library."""
    pages = " ".join(f"{4 + 2 * i} 0 R" for i in range(count))
    objects = ["<< /Type /Catalog /Pages 2 0 R >>", f"<< /Type /Pages /Count {count} /Kids [{pages}] >>",
               "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"]
    for i in range(count):
        stream = f"BT /F1 22 Tf 35 180 Td (SPUR OCR DEMO) Tj 0 -36 Td (Page {i + 1} of {count}) Tj ET"
        objects += [f"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 400 260] /Contents {5 + 2 * i} 0 R "
                    "/Resources << /Font << /F1 3 0 R >> >> >>",
                    f"<< /Length {len(stream)} >>\nstream\n{stream}\nendstream"]
    data, offsets = bytearray(b"%PDF-1.4\n"), [0]
    for n, obj in enumerate(objects, 1):
        offsets.append(len(data))
        data.extend(f"{n} 0 obj\n{obj}\nendobj\n".encode())
    start = len(data)
    data.extend(f"xref\n0 {len(offsets)}\n0000000000 65535 f \n".encode())
    for pos in offsets[1:]:
        data.extend(f"{pos:010d} 00000 n \n".encode())
    data.extend(f"trailer\n<< /Size {len(offsets)} /Root 1 0 R >>\nstartxref\n{start}\n%%EOF\n".encode())
    return bytes(data)


def office(ext):
    """Build a tiny OOXML package for ext from synthetic content; no Office install is needed."""
    base = "http://schemas.openxmlformats.org"
    rel = base + "/officeDocument/2006/relationships"
    pkg = base + "/package/2006/relationships"
    sheet = base + "/spreadsheetml/2006/main"
    slides = base + "/presentationml/2006/main"
    word = base + "/wordprocessingml/2006/main"
    if ext == "docx":
        subtype, main, child, target = "wordprocessingml.document", "word/document.xml", None, None
        content = f'<w:document xmlns:w="{word}"><w:body><w:p><w:r><w:t>Demo</w:t></w:r></w:p></w:body></w:document>'
    elif ext == "xlsx":
        subtype, main, child, target = "spreadsheetml.sheet", "xl/workbook.xml", "worksheet", "worksheets/sheet1.xml"
        content = (f'<workbook xmlns="{sheet}" xmlns:r="{rel}"><sheets>'
                   '<sheet name="Demo" sheetId="1" r:id="rId1"/></sheets></workbook>')
        child_xml = (f'<worksheet xmlns="{sheet}"><sheetData><row r="1"><c r="A1" t="inlineStr">'
                     '<is><t>Demo</t></is></c></row></sheetData></worksheet>')
    else:
        subtype, main, child, target = "presentationml.presentation", "ppt/presentation.xml", "slide", "slides/slide1.xml"
        content = (f'<p:presentation xmlns:p="{slides}" xmlns:r="{rel}"><p:sldIdLst>'
                   '<p:sldId id="256" r:id="rId1"/></p:sldIdLst><p:sldSz cx="9144000" cy="6858000"/>'
                   '<p:notesSz cx="6858000" cy="9144000"/></p:presentation>')
        child_xml = f'<p:sld xmlns:p="{slides}"><p:cSld><p:spTree/></p:cSld></p:sld>'
    mime = "application/vnd.openxmlformats-officedocument." + subtype
    override = f'<Override PartName="/{main}" ContentType="{mime}.main+xml"/>'
    def relationship(kind, path):
        """Link the package part at path using the requested relationship kind."""
        return f'<Relationships xmlns="{pkg}"><Relationship Id="rId1" Type="{rel}/{kind}" Target="{path}"/></Relationships>'
    buffer = io.BytesIO()
    with zipfile.ZipFile(buffer, "w", zipfile.ZIP_DEFLATED) as archive:
        archive.writestr("_rels/.rels", relationship("officeDocument", main))
        archive.writestr(main, content)
        if child:
            folder, name = main.split("/")
            archive.writestr(f"{folder}/_rels/{name}.rels", relationship(child, target))
            archive.writestr(f"{folder}/{target}", child_xml)
            part_type = mime.rsplit(".", 1)[0] + "." + child + "+xml"
            override += f'<Override PartName="/{folder}/{target}" ContentType="{part_type}"/>'
        archive.writestr("[Content_Types].xml", f'<Types xmlns="{base}/package/2006/content-types">'
                         '<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>'
                         f'<Default Extension="xml" ContentType="application/xml"/>{override}</Types>')
    return ext, mime, buffer.getvalue(), 1
