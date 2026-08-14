"""Mechanical classifier for the beta.34 baseline critical-failure records.

Applies Cameron's T-strategy classification rules to every record in
generated-20260813T162453/critical-failures and emits
scripts/baseline/beta34-classification.json plus a cross-check report
against Cameron's golden table.

This is a DERIVATION tool (Parker deliverable 1). It does NOT create
fixtures or the manifest (Quinn owns those).
"""
import json
import os
import re
import sys

REPO = r"C:\my-squad-projects\microsoft-mcp-doc-generation"
SRC = os.path.join(REPO, "generated-20260813T162453", "critical-failures")
OUT = os.path.join(REPO, "scripts", "baseline", "beta34-classification.json")

COVERAGE_RE = re.compile(r"missing '.*?' in example prompt", re.IGNORECASE)
RECON_RE = re.compile(
    r"parameter\(s\) documented but not present in source CLI JSON", re.IGNORECASE
)
GENFAIL_RE = re.compile(r"generation failed", re.IGNORECASE)


def kebab(s: str) -> str:
    s = s.strip().lower()
    s = re.sub(r"[^a-z0-9]+", "-", s)
    return s.strip("-")


def artifact_slug(namespace: str, artifact_name: str) -> str:
    tokens = artifact_name.split()
    if len(tokens) > 1 and tokens[0].lower() == namespace.lower():
        tokens = tokens[1:]
    return kebab("-".join(tokens))


def ordinal_from_filename(fn: str) -> int:
    m = re.search(r"-(\d{2})\.json$", fn)
    if not m:
        raise ValueError(f"cannot parse ordinal from {fn}")
    return int(m.group(1))


def load(path):
    with open(path, "r", encoding="utf-8-sig") as f:
        return json.load(f)


def main():
    files = sorted(f for f in os.listdir(SRC) if f.endswith(".json"))
    records = []
    step2_namespaces = set()
    for fn in files:
        rec = load(os.path.join(SRC, fn))
        if int(rec["stepId"]) == 2:
            step2_namespaces.add(rec["namespace"])
        records.append((fn, rec))

    result = {}
    derived_rows = []
    for fn, rec in records:
        ns = rec["namespace"]
        step = int(rec["stepId"])
        artifact = rec["artifactName"]
        details_text = "\n".join(rec.get("details", []))
        validators = rec.get("validatorResults", [])
        has_coverage = bool(COVERAGE_RE.search(details_text))
        has_recon = bool(RECON_RE.search(details_text))
        is_genfail = bool(GENFAIL_RE.search(details_text))

        # hasUpstreamStep2: a Step 2 critical-failure record exists for same namespace
        has_upstream = step == 4 and ns in step2_namespaces

        # ROLE (priority: diagnostic > mixed > cascade > root)
        if step == 2 and len(validators) == 0 and is_genfail:
            role = "diagnostic"
        elif step == 4 and has_coverage and has_recon:
            role = "mixed"
        elif step == 4 and has_coverage and not has_recon and has_upstream:
            role = "cascade"
        else:
            role = "root"

        # errorClass
        if role == "diagnostic":
            error_class = "C"
        elif role == "mixed":
            error_class = "A+B"
        elif step == 4 and has_recon and not has_coverage:
            error_class = "B"
        else:
            error_class = "A"

        slug = artifact_slug(ns, artifact)
        ordinal = ordinal_from_filename(fn)
        stable_id = f"{ns}.{step:02d}.{slug}.{ordinal:02d}"

        rationale = build_rationale(role, error_class, step, has_coverage,
                                    has_recon, has_upstream, len(validators))

        result[fn] = {
            "stableId": stable_id,
            "classification": role,
            "errorClass": error_class,
            "hasUpstreamStep2": bool(has_upstream) if step == 4 else False,
            "rationale": rationale,
        }
        derived_rows.append((ns, step, artifact, role, error_class, has_upstream))

    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        json.dump(result, f, indent=2, ensure_ascii=False)
        f.write("\n")

    # ---- Reconcile counts ----
    roles = [v["classification"] for v in result.values()]
    classes = [v["errorClass"] for v in result.values()]
    upstream = [v["hasUpstreamStep2"] for v in result.values()]
    print(f"Total records: {len(result)}")
    print("ROLE counts:")
    for r in ("root", "cascade", "mixed", "diagnostic"):
        print(f"  {r:10} = {roles.count(r)}")
    print("errorClass counts:")
    a = classes.count("A")
    b = classes.count("B")
    ab = classes.count("A+B")
    c = classes.count("C")
    print(f"  A={a}  B={b}  A+B(mixed)={ab}  C={c}")
    print(f"  A-total (A + A+B) = {a + ab}")
    print(f"  B-total (B + A+B) = {b + ab}")
    print(f"hasUpstreamStep2 (Class-D pairs) = {sum(1 for u in upstream if u)}")

    # ---- Cross-check vs Cameron's golden table ----
    golden = golden_table()
    print("\n--- DISCREPANCIES vs Cameron golden table ---")
    disc = 0
    for ns, step, artifact, role, error_class, has_up in derived_rows:
        key = (ns, step, artifact)
        if key not in golden:
            print(f"  [MISSING IN GOLDEN] {key}")
            disc += 1
            continue
        g_role, g_class, g_up = golden[key]
        if role != g_role or error_class != g_class or bool(has_up) != bool(g_up):
            print(f"  [MISMATCH] {ns}/{step}/{artifact}: "
                  f"derived=({role},{error_class},up={has_up}) "
                  f"golden=({g_role},{g_class},up={g_up})")
            disc += 1
    print(f"Total discrepancies: {disc}")
    return 0


def build_rationale(role, ec, step, cov, recon, up, nval):
    if role == "diagnostic":
        return ("Step 2 opaque generation failure: empty validatorResults and "
                "'generation failed'/missing-output details, no parameter-identity text (Class C).")
    if role == "mixed":
        return ("Step 4 record whose details carry BOTH a coverage-divergence signature "
                "and a reconstruction-from-rendered-labels signature (Class A+B).")
    if role == "cascade":
        return ("Step 4 coverage-divergence-only failure with an unresolved upstream Step 2 "
                "critical-failure in the same namespace (Class-D dependency cascade, Class A).")
    # root
    if step == 2:
        return ("Step 2 required-parameter identity/coverage divergence with non-empty "
                "validatorResults (originating Class A failure).")
    if recon and not cov:
        return ("Step 4 reconstruction-only failure (canonical name rebuilt from rendered "
                "labels) with no upstream Step 2 in the namespace (originating Class B).")
    return ("Step 4 coverage-divergence failure that originates in Step 4 with no upstream "
            "Step 2 in the namespace (originating Class A).")


def golden_table():
    # (namespace, stepId, artifactName) -> (role, errorClass, hasUpstreamStep2)
    rows = [
        ("appconfig", 2, "appconfig kv get", "root", "A", False),
        ("azurebackup", 2, "azurebackup governance soft-delete", "root", "A", False),
        ("azureterraform", 2, "azureterraform aztfexport query", "root", "A", False),
        ("azureterraform", 2, "azureterraform aztfexport resourcegroup", "root", "A", False),
        ("datadog", 2, "datadog monitoredresources list", "root", "A", False),
        ("foundryextensions", 2, "foundryextensions openai chat-completions-create", "root", "A", False),
        ("foundryextensions", 2, "foundryextensions openai embeddings-create", "root", "A", False),
        ("group", 2, "group resource list", "root", "A", False),
        ("monitor", 2, "monitor webtests get", "diagnostic", "C", False),
        ("search", 2, "search knowledge base retrieve", "root", "A", False),
        ("sreagent", 2, "sreagent docs memories add", "root", "A", False),
        ("sreagent", 2, "sreagent docs memories search", "root", "A", False),
        ("sreagent", 2, "sreagent threads send message", "root", "A", False),
        ("storage", 2, "storage account create", "root", "A", False),
        ("storage", 2, "storage blob container create", "root", "A", False),
        ("storagesync", 2, "storagesync cloudendpoint changedetection", "root", "A", False),
        ("storagesync", 2, "storagesync cloudendpoint create", "root", "A", False),
        ("appconfig", 4, "appconfig", "cascade", "A", True),
        ("azurebackup", 4, "azurebackup", "cascade", "A", True),
        ("azureterraform", 4, "azureterraform", "cascade", "A", True),
        ("compute", 4, "compute", "root", "B", False),
        ("cosmos", 4, "cosmos", "root", "B", False),
        ("datadog", 4, "datadog", "cascade", "A", True),
        ("eventhubs", 4, "eventhubs", "root", "A", False),
        ("foundryextensions", 4, "foundryextensions", "mixed", "A+B", True),
        ("group", 4, "group", "cascade", "A", True),
        ("loadtesting", 4, "loadtesting", "mixed", "A+B", False),
        ("mysql", 4, "mysql", "root", "B", False),
        ("postgres", 4, "postgres", "mixed", "A+B", False),
        ("search", 4, "search", "cascade", "A", True),
        ("sreagent", 4, "sreagent", "cascade", "A", True),
        ("storage", 4, "storage", "cascade", "A", True),
        ("storagesync", 4, "storagesync", "cascade", "A", True),
        ("virtualdesktop", 4, "virtualdesktop", "root", "B", False),
    ]
    return {(ns, st, an): (r, ec, up) for ns, st, an, r, ec, up in rows}


if __name__ == "__main__":
    sys.exit(main())
