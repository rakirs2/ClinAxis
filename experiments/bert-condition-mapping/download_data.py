import requests
import pandas as pd
import sys
from config import TRAINING_URL, RAW_CSV, CANONICAL_CSV

def download_training_data(url: str) -> pd.DataFrame:
    print(f"Downloading training data from {url} ...")
    resp = requests.get(url, timeout=120)
    resp.raise_for_status()
    lines = resp.text.strip().split("\n")
    print(f"  Received {len(lines) - 1} rows")
    rows = []
    for line in lines[1:]:
        if not line.strip():
            continue
        parts = []
        in_quotes = False
        current = []
        for ch in line:
            if ch == '"':
                in_quotes = not in_quotes
            elif ch == ',' and not in_quotes:
                parts.append("".join(current))
                current = []
            else:
                current.append(ch)
        parts.append("".join(current))
        if len(parts) >= 3:
            value = parts[0].strip('"')
            label = int(parts[1])
            nct_id = parts[2].strip('"')
            rows.append({"value": value, "label": label, "study_nct_id": nct_id})
    df = pd.DataFrame(rows)
    print(f"  Accepted (label=1): {(df['label'] == 1).sum()}")
    print(f"  Rejected (label=0): {(df['label'] == 0).sum()}")
    return df

def build_canonical_list() -> list[str]:
    print("\nBuilding canonical condition reference list ...")
    conditions = [
        "Diabetes Mellitus", "Type 2 Diabetes", "Type 1 Diabetes",
        "Hypertension", "Essential Hypertension",
        "Heart Failure", "Congestive Heart Failure",
        "Coronary Artery Disease", "Myocardial Infarction",
        "Atrial Fibrillation", "Stroke", "Cerebrovascular Accident",
        "Asthma", "Chronic Obstructive Pulmonary Disease", "COPD",
        "Pneumonia", "COVID-19", "Influenza",
        "Breast Cancer", "Lung Cancer", "Colorectal Cancer",
        "Prostate Cancer", "Pancreatic Cancer", "Ovarian Cancer",
        "Melanoma", "Leukemia", "Lymphoma",
        "HIV Infections", "Hepatitis B", "Hepatitis C",
        "Cirrhosis", "Non-Alcoholic Fatty Liver Disease",
        "Chronic Kidney Disease", "Acute Kidney Injury",
        "Osteoarthritis", "Rheumatoid Arthritis",
        "Osteoporosis", "Back Pain", "Sciatica",
        "Major Depressive Disorder", "Depression",
        "Anxiety Disorders", "Generalized Anxiety Disorder",
        "Bipolar Disorder", "Schizophrenia",
        "Post-Traumatic Stress Disorder", "PTSD",
        "Attention Deficit Hyperactivity Disorder", "ADHD",
        "Alzheimer Disease", "Parkinson Disease",
        "Multiple Sclerosis", "Epilepsy", "Migraine",
        "Concussion", "Traumatic Brain Injury", "TBI",
        "Spinal Cord Injury", "SCI",
        "Crohn Disease", "Ulcerative Colitis",
        "Irritable Bowel Syndrome", "IBS",
        "Obesity", "Metabolic Syndrome",
        "Hypothyroidism", "Hyperthyroidism",
        "Anemia", "Sickle Cell Disease",
        "Cystic Fibrosis", "Pulmonary Fibrosis",
        "Glaucoma", "Cataract", "Macular Degeneration",
        "Psoriasis", "Eczema", "Dermatitis",
        "Urinary Tract Infection", "UTI",
        "Sepsis", "Septic Shock",
        "Systemic Lupus Erythematosus", "SLE",
        "Sarcoma", "Glioblastoma", "Mesothelioma",
        "Endometriosis", "Uterine Fibroids",
        "Gastroesophageal Reflux Disease", "GERD",
        "Peptic Ulcer Disease", "Gastritis",
        "Diverticulitis", "Appendicitis",
        "Pancreatitis", "Cholecystitis",
        "Deep Vein Thrombosis", "Pulmonary Embolism",
        "Peripheral Artery Disease", "Aortic Stenosis",
        "Cardiomyopathy", "Myocarditis", "Pericarditis",
        "Encephalitis", "Meningitis",
        "Tuberculosis", "Malaria",
        "Chronic Pain", "Neuropathic Pain",
        "Insomnia", "Sleep Apnea", "OSA",
        "Autism Spectrum Disorder", "Autism",
        "Cerebral Palsy", "Spina Bifida",
        "Down Syndrome", "Fragile X Syndrome",
        "Gout", "Pseudogout",
        "Sarcoidosis", "Amyloidosis",
        "Hemophilia", "von Willebrand Disease",
        "Polycystic Ovary Syndrome", "PCOS",
        "Erectile Dysfunction", "Benign Prostatic Hyperplasia",
        "Glomerulonephritis", "Nephrotic Syndrome",
        "Primary Biliary Cholangitis", "Primary Sclerosing Cholangitis",
        "Achalasia", "Barrett Esophagus",
        "Trigeminal Neuralgia", "Bell Palsy",
        "Meniere Disease", "Tinnitus",
        "Fibromyalgia", "Chronic Fatigue Syndrome",
        "Sinusitis", "Rhinitis", "Tonsillitis",
        "Cellulitis", "Abscess",
        "Dehydration", "Malnutrition",
        "Vitamin D Deficiency", "Iron Deficiency Anemia",
        "Graft vs Host Disease", "Transplant Rejection",
        "Allergic Rhinitis", "Anaphylaxis",
        "Angioedema", "Urticaria",
        "Inflammation", "Chronic Inflammation",
        "Nonalcoholic Steatohepatitis", "NASH",
        "Primary Hypertension", "Pulmonary Hypertension",
        "Hypoglycemia", "Hyperglycemia",
        "Hyperlipidemia", "Hypercholesterolemia",
        "Fatty Liver", "Alcoholic Hepatitis",
        "Acute Respiratory Distress Syndrome", "ARDS",
        "Bronchitis", "Bronchiolitis",
        "Laryngitis", "Pharyngitis",
        "Otitis Media", "Otitis Externa",
        "Conjunctivitis", "Keratitis",
        "Iritis", "Uveitis",
        "Vasculitis", "Polyarteritis Nodosa",
        "Dermatomyositis", "Polymyositis",
        "Scleroderma", "Sjogren Syndrome",
        "Ankylosing Spondylitis", "Psoriatic Arthritis",
        "Reactive Arthritis", "Juvenile Idiopathic Arthritis",
        "Hemochromatosis", "Wilson Disease",
        "Porphyria", "G6PD Deficiency",
        "Marfan Syndrome", "Ehlers-Danlos Syndrome",
        "Huntington Disease", "Amyotrophic Lateral Sclerosis", "ALS",
        "Spinal Muscular Atrophy", "Muscular Dystrophy",
        "Myasthenia Gravis", "Guillain-Barre Syndrome",
        "Neurofibromatosis", "Tuberous Sclerosis",
        "Substance Use Disorder", "Alcohol Use Disorder",
        "Opioid Use Disorder", "Nicotine Dependence",
        "Eating Disorders", "Anorexia Nervosa", "Bulimia Nervosa",
        "Borderline Personality Disorder", "Narcissistic Personality Disorder",
        "Panic Disorder", "Agoraphobia", "Social Anxiety Disorder",
        "Obsessive Compulsive Disorder", "OCD",
        "Seasonal Affective Disorder", "Postpartum Depression",
        "Premenstrual Dysphoric Disorder", "PMDD",
        "Dementia", "Vascular Dementia", "Lewy Body Dementia",
        "Frontotemporal Dementia", "Mild Cognitive Impairment",
        "Normal Pressure Hydrocephalus", "Hydrocephalus",
        "Cervical Dystonia", "Essential Tremor",
        "Restless Legs Syndrome", "Narcolepsy",
        "Congenital Heart Disease", "Ventricular Septal Defect",
        "Atrial Septal Defect", "Patent Ductus Arteriosus",
        "Tetralogy of Fallot", "Coarctation of the Aorta",
        "Cleft Lip", "Cleft Palate",
        "Hernia", "Hiatal Hernia", "Inguinal Hernia",
        "Hemorrhoids", "Anal Fissure", "Fistula",
        "Gallstones", "Kidney Stones", "Ureteral Stones",
        "Male Hypogonadism", "Menopause",
        "Endometrial Hyperplasia", "Cervical Dysplasia",
        "Gestational Diabetes", "Preeclampsia",
        "Miscarriage", "Recurrent Pregnancy Loss",
        "Preterm Labor", "Preterm Birth",
        "Infertility", "Female Infertility", "Male Infertility",
    ]
    print(f"  {len(conditions)} canonical conditions")
    return conditions


if __name__ == "__main__":
    import os
    os.makedirs("data", exist_ok=True)

    df = download_training_data(TRAINING_URL)
    df.to_csv(RAW_CSV, index=False)
    print(f"  Saved {len(df)} rows to {RAW_CSV}")

    conditions = build_canonical_list()
    canon_df = pd.DataFrame({"condition": conditions})
    canon_df.to_csv(CANONICAL_CSV, index=False)
    print(f"  Saved {len(conditions)} canonical conditions to {CANONICAL_CSV}")
