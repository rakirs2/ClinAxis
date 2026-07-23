import requests
import xml.etree.ElementTree as ET
import json
import os
import gzip
import io
from config import MESH_DIR, MESH_DESC_FILE, MESH_TREES_FILE, MESH_NAMES_FILE


MESH_XML_URL = "https://nlmpubs.nlm.nih.gov/projects/mesh/MESH_FILES/xmlmesh/desc2026.xml"


TREE_CATEGORY_MAP = {
    "A": "anatomy",
    "B": "organism",
    "C": "disease",
    "D": "chemical",
    "E": "procedure",
    "F": "disorder",
    "G": "procedure",
    "H": "procedure",
    "I": "anthropology",
    "J": "technology",
    "K": "humanities",
    "L": "information",
    "M": "named_group",
    "N": "healthcare",
    "V": "publication",
    "Z": "geography",
}


def download_file(url, dest, desc="file"):
    if os.path.exists(dest):
        print(f"  {desc} already cached at {dest}")
        return
    print(f"  Downloading {desc} from {url} ...")
    resp = requests.get(url, timeout=300)
    resp.raise_for_status()
    os.makedirs(os.path.dirname(dest), exist_ok=True)
    with open(dest, "wb") as f:
        f.write(resp.content)
    size_mb = len(resp.content) / (1024 * 1024)
    print(f"    {size_mb:.1f} MB downloaded")


def parse_descriptors(xml_path: str) -> tuple[dict[str, dict], dict[str, list[str]]]:
    print(f"  Parsing MeSH descriptors from {xml_path} ...")
    descriptors = {}
    trees = {}
    tree = ET.parse(xml_path)
    root = tree.getroot()

    for desc in root.findall(".//DescriptorRecord"):
        ui_elem = desc.find("DescriptorUI")
        name_elem = desc.find("DescriptorName/String")
        if ui_elem is None or name_elem is None:
            continue
        cui = ui_elem.text
        name = name_elem.text

        tree_numbers = []
        for tn in desc.findall(".//TreeNumber"):
            if tn.text:
                tree_numbers.append(tn.text.strip())
        if tree_numbers:
            trees[cui] = tree_numbers

        concepts = []
        seen = set()
        for concept in desc.findall(".//Concept"):
            cname = concept.find("ConceptName/String")
            preferred = concept.find("ConceptPreferredNameYN")
            if cname is not None and preferred is not None and preferred.text == "Y":
                concepts.insert(0, cname.text)
                seen.add(cname.text)
            elif cname is not None:
                concepts.append(cname.text)
                seen.add(cname.text)
            for term in concept.findall(".//Term"):
                ts = term.find("String")
                if ts is not None and ts.text and ts.text not in seen:
                    concepts.append(ts.text)
                    seen.add(ts.text)

        descriptors[cui] = {
            "cui": cui,
            "name": name,
            "synonyms": concepts,
        }

    print(f"    {len(descriptors)} descriptors parsed")
    print(f"    {len(trees)} descriptors with tree numbers")
    return descriptors, trees


def get_category(tree_numbers: list[str]) -> str:
    for tn in tree_numbers:
        if tn.startswith("F03"):
            return "disease"
        first_letter = tn[0] if tn else ""
        if first_letter in TREE_CATEGORY_MAP:
            return TREE_CATEGORY_MAP[first_letter]
    return "other"


def get_all_names_and_categories(
    descriptors: dict[str, dict],
    trees: dict[str, list[str]],
) -> tuple[list[str], list[str], list[list[str]], list[str]]:
    names = []
    cuis = []
    tree_list = []
    categories = []
    for cui, desc in descriptors.items():
        name = desc["name"]
        tn_list = trees.get(cui, [])
        cat = get_category(tn_list)
        names.append(name)
        cuis.append(cui)
        tree_list.append(tn_list)
        categories.append(cat)
        for syn in desc["synonyms"]:
            if syn != name:
                names.append(syn)
                cuis.append(cui)
                tree_list.append(tn_list)
                categories.append(cat)
    return names, cuis, tree_list, categories


def download():
    os.makedirs(MESH_DIR, exist_ok=True)

    print("\n--- Downloading MeSH Data ---")
    download_file(MESH_XML_URL, MESH_DESC_FILE, "MeSH descriptors XML")

    descriptors, trees = parse_descriptors(MESH_DESC_FILE)

    names, cuis, tree_list, categories = get_all_names_and_categories(descriptors, trees)

    index_data = {
        "names": names,
        "cuis": cuis,
        "tree_numbers": tree_list,
        "categories": categories,
    }

    import pickle
    with open(os.path.join(MESH_DIR, "index.pkl"), "wb") as f:
        pickle.dump(index_data, f)

    print(f"  Saved {len(names)} total MeSH terms (with synonyms) to index.pkl")
    print(f"    Categories: disease={categories.count('disease')}, "
          f"procedure={categories.count('procedure')}, "
          f"other={len(categories) - categories.count('disease') - categories.count('procedure')}")

    return index_data


if __name__ == "__main__":
    download()
