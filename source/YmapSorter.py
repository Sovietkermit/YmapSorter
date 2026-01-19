import xml.etree.ElementTree as ET
import tkinter as tk
from tkinter import filedialog, messagebox
import os
import sys

CURRENT_LANG = "fr"
ICON_NAME = "ico_ymap.ico"

texts = {
    "fr": {
        "app_title": "Trieur d'Entités YMAP",
        "header": "Trieur d'Entités (CodeWalker XML)",
        "instructions": "IMPORTANT : Exportez votre .ymap en .xml via CodeWalker d'abord.\nCe logiciel ne lit pas les .ymap binaires du jeu.",
        "btn_browse": "Ouvrir le fichier XML",
        "wait": "En attente d'un fichier...",
        "lang_btn": "🇬🇧 English",
        "success": "Succès ! {count} entités triées.\nSauvegardé dans :\n{path}",
        "err_binary_title": "Format binaire détecté",
        "err_binary_msg": "Ce fichier est un .ymap binaire (format jeu).\n\nLe script ne traite que le TEXTE.\n1. Ouvrez-le avec CodeWalker.\n2. Faites 'File > Export XML'.\n3. Utilisez le fichier XML exporté.",
        "err_struct_title": "Erreur Structure",
        "err_struct_msg": "Balise <entities> introuvable.\nAssurez-vous que c'est bien un export XML CodeWalker.",
        "err_xml_title": "Erreur XML",
        "err_xml_msg": "Le fichier contient des erreurs de syntaxe XML.\nEst-il corrompu ?",
        "cancelled": "Opération annulée."
    },
    "en": {
        "app_title": "YMAP Entity Sorter",
        "header": "Entity Sorter (CodeWalker XML)",
        "instructions": "IMPORTANT: Export your .ymap to .xml via CodeWalker first.\nThis software does not read binary game .ymap files.",
        "btn_browse": "Open XML File",
        "wait": "Waiting for file...",
        "lang_btn": "🇫🇷 Français",
        "success": "Success! {count} entities sorted.\nSaved in:\n{path}",
        "err_binary_title": "Binary Format Detected",
        "err_binary_msg": "This file appears to be a binary .ymap.\n\nThis script only processes TEXT.\n1. Open it with CodeWalker.\n2. Go to 'File > Export XML'.\n3. Use the exported XML file.",
        "err_struct_title": "Structure Error",
        "err_struct_msg": "<entities> tag not found.\nPlease ensure this is a valid CodeWalker XML export.",
        "err_xml_title": "XML Error",
        "err_xml_msg": "The file contains XML syntax errors.\nIs it corrupt?",
        "cancelled": "Operation cancelled."
    }
}

def get_app_path():
    """Retourne le dossier où se trouve l'exécutable ou le script"""
    if getattr(sys, 'frozen', False):
        return os.path.dirname(sys.executable)
    else:
        return os.path.dirname(os.path.abspath(__file__))

def resource_path(relative_path):
    """Retourne le chemin absolu des ressources, pour le dév et pour PyInstaller"""
    try:
        base_path = sys._MEIPASS
    except Exception:
        base_path = os.path.abspath(".")

    return os.path.join(base_path, relative_path)

def update_ui_text():
    t = texts[CURRENT_LANG]
    root.title(t["app_title"])
    label_title.config(text=t["header"])
    lbl_instruction.config(text=t["instructions"])
    btn_browse.config(text=t["btn_browse"])
    btn_lang.config(text=t["lang_btn"])
    
    current_result_text = lbl_result.cget("text")
    if "..." in current_result_text or current_result_text == "":
        lbl_result.config(text=t["wait"])

def toggle_language():
    global CURRENT_LANG
    CURRENT_LANG = "en" if CURRENT_LANG == "fr" else "fr"
    update_ui_text()

def sort_xml_by_archetype(file_path):
    t = texts[CURRENT_LANG]
    try:
        with open(file_path, 'r', encoding='utf-8', errors='ignore') as f:
            header = f.read(50)
            if "<?xml" not in header and "<Item" not in header and "<CMapData" not in header:
                messagebox.showwarning(t["err_binary_title"], t["err_binary_msg"])
                return None

        tree = ET.parse(file_path)
        root_xml = tree.getroot()

        entities_section = root_xml.find('entities')
        if entities_section is None and root_xml.tag == "CMapData":
            entities_section = root_xml.find('entities')
            
        if entities_section is None:
            messagebox.showerror(t["err_struct_title"], t["err_struct_msg"])
            return None

        items = entities_section.findall('Item')
        items_to_sort = []
        items_others = []
        count_sorted = 0

        for item in items:
            archetype = item.find('archetypeName')
            if (archetype is not None) and (archetype.text is not None):
                items_to_sort.append(item)
                count_sorted += 1
            else:
                items_others.append(item)

        items_to_sort.sort(key=lambda x: x.find('archetypeName').text.lower())

        for item in list(entities_section):
            entities_section.remove(item)
        for item in items_to_sort:
            entities_section.append(item)
        for item in items_others:
            entities_section.append(item)

        ET.indent(tree, space="  ", level=0)

        app_dir = get_app_path()
        export_dir = os.path.join(app_dir, "export")
        if not os.path.exists(export_dir):
            os.makedirs(export_dir)

        filename = os.path.basename(file_path)
        final_output_path = os.path.join(export_dir, filename)
        tree.write(final_output_path, encoding="UTF-8", xml_declaration=True)

        return (final_output_path, count_sorted)

    except ET.ParseError:
        messagebox.showerror(t["err_xml_title"], t["err_xml_msg"])
        return None
    except Exception as e:
        messagebox.showerror("Error", f"{str(e)}")
        return None

def select_file():
    t = texts[CURRENT_LANG]
    file_path = filedialog.askopenfilename(title=t["btn_browse"], filetypes=[("CodeWalker XML", "*.xml *.ymap")])
    if file_path:
        result = sort_xml_by_archetype(file_path)
        if result:
            path, count = result
            display_path = f".../export/{os.path.basename(path)}"
            msg = t["success"].format(count=count, path=display_path)
            lbl_result.config(text=msg, fg="#008800")
        else:
            lbl_result.config(text=t["cancelled"], fg="red")

root = tk.Tk()
root.geometry("500x300")

try:
    root.iconbitmap(resource_path(ICON_NAME))
except Exception:
    pass

top_frame = tk.Frame(root)
top_frame.pack(fill="x", padx=10, pady=5)

btn_lang = tk.Button(top_frame, text="🇬🇧 English", command=toggle_language, bg="#f0f0f0", relief="groove", font=("Segoe UI", 8))
btn_lang.pack(side="right")

label_title = tk.Label(root, text="", font=("Segoe UI", 14, "bold"))
label_title.pack(pady=(5, 5))

lbl_instruction = tk.Label(root, text="", font=("Segoe UI", 9), fg="#555555")
lbl_instruction.pack(pady=5)

btn_browse = tk.Button(root, text="", command=select_file, height=2, width=25, bg="#e1e1e1", font=("Segoe UI", 10))
btn_browse.pack(pady=20)

lbl_result = tk.Label(root, text="", font=("Segoe UI", 9, "italic"), fg="grey")
lbl_result.pack(pady=10)

update_ui_text()
root.mainloop()