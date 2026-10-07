"""Mechanical Java -> C# translation of material-color-utilities (java/).

The output keeps upstream class and member names so that every file can be
diffed against its Java original. Constructs that need judgement are
finished by hand afterwards (see tools/README.md); this script only does the
parts that are safe to do with text rules.

usage: python java2cs.py <mcu java dir> <output dir>
"""
import os
import re
import sys

SRC, DST = sys.argv[1], sys.argv[2]
NS = "M3E4Unity.MaterialColor"

# enum member -> owning C# type, used for unqualified case labels / static imports
ENUMS = {}


def collect_enums(text):
    for m in re.finditer(r"enum\s+(\w+)\s*\{([^}]*)\}", text):
        owner = m.group(1)
        outer = re.search(r"^public (?:final )?(?:class|interface)\s+(\w+)", text[: m.start()], re.M)
        full = f"{outer.group(1)}.{owner}" if outer and outer.group(1) != owner else owner
        for member in re.findall(r"\b([A-Z][A-Z0-9_]+)\b", m.group(2)):
            ENUMS.setdefault(member, set()).add(full)


def find_block(text, open_idx):
    """index of the brace matching text[open_idx] == '{'"""
    depth = 0
    i = open_idx
    while i < len(text):
        c = text[i]
        if c == "{":
            depth += 1
        elif c == "}":
            depth -= 1
            if depth == 0:
                return i
        elif c == '"':
            i = text.index('"', i + 1)
        i += 1
    raise ValueError("unbalanced")


def split_arms(body):
    """split a Java arrow-switch body into (labels, arm-text) at top level"""
    arms = []
    i, depth = 0, 0
    starts = []
    while i < len(body):
        c = body[i]
        if c in "({[":
            depth += 1
        elif c in ")}]":
            depth -= 1
        elif depth == 0 and (body.startswith("case ", i) or body.startswith("default", i)):
            if i == 0 or not (body[i - 1].isalnum() or body[i - 1] == "_"):
                starts.append(i)
        i += 1
    starts.append(len(body))
    for a, b in zip(starts, starts[1:]):
        chunk = body[a:b].strip()
        arrow = chunk.index("->")
        labels = chunk[:arrow].strip()
        arm = chunk[arrow + 2 :].strip()
        arms.append((labels, arm))
    return arms


def enum_type_for(label, subject):
    owners = ENUMS.get(label, set())
    if len(owners) == 1:
        return next(iter(owners))
    hints = {"constraint": "ToneDeltaPair.DeltaConstraint", "polarity": "TonePolarity"}
    for k, v in hints.items():
        if k in subject.lower():
            return v
    return None


def case_labels(labels, subject):
    if labels.startswith("default"):
        return "default:"
    names = [n.strip() for n in labels[len("case ") :].split(",") if n.strip()]
    out = []
    for n in names:
        t = enum_type_for(n, subject) if re.fullmatch(r"[A-Z][A-Z0-9_]*", n) else None
        out.append(f"case {t}.{n}:" if t else f"case {n}:")
    return " ".join(out)


def convert_switches(text):
    pat = re.compile(r"(return\s+|(\w+)\s*=\s*)?switch\s*\(([^)]*)\)\s*\{")
    pos = 0
    while True:
        m = pat.search(text, pos)
        if not m:
            return text
        open_idx = m.end() - 1
        close_idx = find_block(text, open_idx)
        body = text[open_idx + 1 : close_idx]
        if "->" not in body.split("\n", 2)[1] and not re.search(r"^\s*(case [^:]*|default)\s*->", body, re.M):
            pos = m.end()
            continue
        subject = m.group(3)
        arms = split_arms(body)
        is_return = m.group(1) and m.group(1).startswith("return")
        assign = m.group(2) if m.group(1) and not is_return else None
        lines = [f"switch ({subject}) {{"]
        for labels, arm in arms:
            head = case_labels(labels, subject)
            if arm.startswith("{"):
                inner = arm[1 : find_block(arm, 0)]
                lines.append(f"{head} {{{inner}}}\n break;")
            else:
                expr = arm.rstrip(";").strip()
                if expr.startswith("throw"):
                    lines.append(f"{head} {expr};")
                elif is_return:
                    lines.append(f"{head} return {expr};")
                elif assign:
                    lines.append(f"{head} {assign} = {expr}; break;")
                else:
                    lines.append(f"{head} {expr}; break;")
        if is_return and not any(l.startswith("default") for l, _ in arms):
            lines.append("default: throw new ArgumentOutOfRangeException();")
        lines.append("}")
        replacement = "\n".join(lines)
        end = close_idx + 1
        # an expression switch is terminated by ';'
        if m.group(1):
            semi = re.match(r"\s*;", text[end:])
            if semi:
                end += semi.end()
        text = text[: m.start()] + replacement + text[end:]
        pos = m.start() + len(replacement)


def static_imports(text):
    imports = re.findall(r"^import static ([\w.]+)\.(\w+);", text, re.M)
    for path, member in imports:
        owner = path.split(".")[-1]
        if path.startswith("java.lang.Math"):
            owner = "JMath"
        elif path.startswith("java.util.stream.Collectors"):
            continue
        else:
            parts = path.split(".")
            # dynamiccolor.DynamicScheme.Platform -> DynamicScheme.Platform
            owner = ".".join(p for p in parts[1:]) if len(parts) > 2 else parts[-1]
        head, sep, rest = text.partition("\npublic ")
        if not sep:
            head, sep, rest = text.partition("\nclass ")
        if not sep:
            head, sep, rest = text.partition("\nfinal class ")
        rest = re.sub(rf"(?<![\w.\"]){member}\b(?!\s*=[^=])", f"{owner}.{member}", rest)
        text = head + sep + rest
    return text


INTERFACES = set()

MEMBER_START = re.compile(
    r"^(\s*)(?!(?:return|throw|new|else|if|for|while|switch|case|default|public|private|protected|internal|do|try|catch|finally|break|continue|this|super|assert|override|@)\b)"
    r"((?:static |abstract )?(?:[A-Za-z_][\w<>\[\], .?]*?\s+)?[A-Za-z_]\w*\s*(?:\(|=|;))"
)


def package_private_to_internal(text):
    """Java members with no access modifier are package-private; C# would make them private."""
    out = []
    stack = []  # 'type' or 'block'
    pending_header = ""
    in_comment = False
    for line in text.split("\n"):
        stripped = line.strip()
        code = line
        if in_comment:
            if "*/" in line:
                in_comment = False
            out.append(line)
            continue
        if stripped.startswith("/*") and "*/" not in stripped:
            in_comment = True
            out.append(line)
            continue
        if stripped.startswith("//") or stripped.startswith("*") or stripped.startswith("/*"):
            out.append(line)
            continue
        in_type_body = bool(stack) and stack[-1] == "type"
        if in_type_body and not pending_header.strip():
            m = MEMBER_START.match(line)
            if m and not re.match(r"^\s*[A-Z][A-Z0-9_]*\s*[,(;]?\s*$", line):  # not an enum constant
                line = f"{m.group(1)}internal {line[len(m.group(1)):]}"
            elif re.match(r"^\s*(static\s+)?(enum|class|interface)\b", line):
                line = re.sub(r"^(\s*)", r"\1internal ", line, count=1)
        # strip strings/char literals before counting braces
        scan = re.sub(r'"(?:\\.|[^"\\])*"', '""', code)
        scan = re.sub(r"'(?:\\.|[^'\\])'", "''", scan)
        scan = scan.split("//")[0]
        for ch in scan:
            if ch == "{":
                header = pending_header + scan[: scan.index("{")]
                kind = re.search(r"\b(class|interface|enum)\s+\w+[^=(]*$", header)
                stack.append(("iface" if kind.group(1) == "interface" else "type") if kind else "block")
                pending_header = ""
            elif ch == "}":
                if stack:
                    stack.pop()
                pending_header = ""
        if "{" not in scan and "}" not in scan:
            if scan.strip().endswith(";"):
                pending_header = ""
            else:
                pending_header += " " + scan
        out.append(line)
    return "\n".join(out)


def convert(text, file_name):
    text = text.replace("\r\n", "\n")
    text = static_imports(text)
    # import pkg.Outer.Inner; -> using Inner = NS.Outer.Inner;
    aliases = []
    for outer, inner in re.findall(r"^import (?!static)(?!java)\w+\.([A-Z]\w*)\.([A-Z]\w*);", text, re.M):
        aliases.append(f"using {inner} = {NS}.{outer}.{inner};")
    # @Override: implementing an interface -> virtual, overriding a class -> override
    m = re.search(r"class\s+\w+\s+extends\s+(\w+)", text)
    parent_is_class = bool(m) and m.group(1) not in INTERFACES
    text = re.sub(r"@Override\s+public (?:boolean|bool) equals", "public boolean equals", text)
    text = re.sub(r"@Override\s+public int hashCode", "public int hashCode", text)
    text = re.sub(r"@Override\s+public String toString", "public String toString", text)
    if not parent_is_class:
        sealed = re.search(r"\bfinal class\b", text) is not None
        text = re.sub(r"@Override\s+public\s+", "public " if sealed else "public virtual ", text)
    text = re.sub(r"\bstatic final class\b", "sealed class", text)
    text = re.sub(r"\bstatic class\b", "class", text)
    text = re.sub(r"^package [\w.]+;\n", "", text, flags=re.M)
    text = re.sub(r"^import [^\n]+;\n", "", text, flags=re.M)
    text = re.sub(r"@(NonNull|Nullable|CanIgnoreReturnValue|CheckReturnValue|SuppressWarnings\([^)]*\)|FunctionalInterface|Deprecated|InlineMe\([^)]*\)|Var)\s+", "", text)
    text = convert_switches(text)

    # C# keywords used as Java identifiers
    for kw in ("base", "object", "params", "in", "out", "ref", "checked", "fixed", "lock", "event", "operator"):
        text = re.sub(rf"(?<![\w.@]){kw}\b(?!\s*\.)", f"@{kw}", text) if kw == "base" else re.sub(rf"(?<![\w.@\"]){kw}\b(?=\s*[,);=.\[]|\s*$|\s+instanceof)", f"@{kw}", text, flags=re.M)
    text = re.sub(r"\bObject\b", "object", text)
    # Java "3." double literals
    text = re.sub(r"(?<![\w.])(\d+)\.(?![\d\w])", r"\1.0", text)
    # trailing ';' after the last enum constant
    text = re.sub(r"(\n\s*[A-Z][A-Z0-9_]*)\s*;(\s*\n\s*\}\s*$)", r"\1\2", text) if re.search(r"^public enum", text, re.M) else text
    # Map.Entry iteration
    text = re.sub(r"for\s*\(\s*Map\.Entry<([^>]+)>\s+(\w+)\s*:\s*([\w.()]+?)\.entrySet\(\)\s*\)", r"foreach (KeyValuePair<\1> \2 in \3)", text)
    text = re.sub(r"\.getKey\(\)", ".Key", text)
    text = re.sub(r"\.getValue\(\)", ".Value", text)
    text = re.sub(r"public (?:boolean|bool) equals\((?:Object|object) (@?\w+)\)", r"public override bool Equals(object \1)", text)
    text = re.sub(r"public int hashCode\(\)", "public override int GetHashCode()", text)

    # constructor chaining: Foo(...) { this(...); -> Foo(...) : this(...) {
    text = re.sub(r"\)\s*\{\s*this\(((?:[^;])*?)\);", r") : this(\1) {", text)
    text = re.sub(r"\)\s*\{\s*super\(((?:[^;])*?)\);", r") : base(\1) {", text)

    text = package_private_to_internal(text)
    text = re.sub(r"\bprivate (sealed class|class|enum|static class)\b", r"internal \1", text)

    # 2D array initializers: T[][] x = { {..}, {..} };
    def arr2d(m):
        t, name, body = m.group(1), m.group(2), m.group(3)
        rows = re.sub(r"\{", f"new {t}[] {{", body)
        return f"{t}[][] {name} = new {t}[][] {{{rows}}};"

    text = re.sub(r"(\w+)\[\]\[\]\s+(\w+)\s*=\s*\{((?:\s*\{[^{}]*\}\s*,?)+\s*)\};", arr2d, text)

    # diamond operator -> target-typed new
    text = re.sub(r"\bnew\s+(?:ArrayList|HashMap|LinkedHashMap|HashSet|TreeMap)<>\(\)", "new()", text)
    text = re.sub(r"\bnew\s+(?:ArrayList|HashMap|LinkedHashMap|HashSet|TreeMap)<>\(((?:[^()]|\([^()]*\))+)\)", r"new(\1)", text)
    # 1.f / 0.f float literals
    text = re.sub(r"(?<![\w.])(\d+)\.f\b", r"\1.0f", text)
    text = re.sub(r"\s*>>>\s*0\s*;", ";", text)
    text = re.sub(r"\bUnsupportedOperationException\b", "NotSupportedException", text)
    text = re.sub(r"\bnew Random\(", "new JavaRandom(", text)
    text = re.sub(r"\bRandom\s+(\w+)\s*=", r"JavaRandom \1 =", text)
    text = re.sub(r"\bOptional\.empty\(\)", "Optional<TonalPalette>.empty()", text)
    text = re.sub(r"\bOptional\.of\(", "Optional<TonalPalette>.of(", text)

    # lambdas and method references
    text = text.replace("->", "=>")
    text = re.sub(r"\bthis::", "", text)
    text = re.sub(r"\b(\w+)::(\w+)", r"\1.\2", text)

    # types
    reps = [
        (r"\bboolean\b", "bool"),
        (r"\bString\b", "string"),
        (r"\bFunction<", "Func<"),
        (r"\bBiFunction<", "Func<"),
        (r"\bSupplier<", "Func<"),
        (r"\bDouble\b", "double"),
        (r"\bInteger\b", "int"),
        (r"\bBoolean\b", "bool"),
        (r"\bArrayList<", "List<"),
        (r"\bHashMap<", "Dictionary<"),
        (r"\bLinkedHashMap<", "Dictionary<"),
        (r"\bMap<", "Dictionary<"),
        (r"\bSet<", "HashSet<"),
        (r"\bHashHashSet<", "HashSet<"),
        (r"\bHashSet<", "HashSet<"),
        (r"\bIllegalArgumentException\b", "ArgumentException"),
        (r"\bIllegalStateException\b", "InvalidOperationException"),
        (r"\bMath\.", "JMath."),
        (r"\.apply\(", "("),
        (r"\.equals\(", ".Equals("),
        (r"\bpublic String toString\(\)", "public override string ToString()"),
        (r"\.size\(\)", ".Count"),
        (r"\.length\(\)", ".Length"),
        (r"\.length\b", ".Length"),
        (r"\.isEmpty\(\)", ".Count == 0"),
        (r"\.add\(", ".Add("),
        (r"\.put\(", "[__PUT__("),
        (r"\bfinal class\b", "sealed class"),
        (r"\bstatic final class\b", "sealed class"),
        (r"\bstatic class\b", "class"),
        (r"\bpublic final\b", "public readonly"),
        (r"\bprivate final\b", "private readonly"),
        (r"\bprotected final\b", "protected readonly"),
        (r"\bprivate static final\b", "private static readonly"),
        (r"\bpublic static final\b", "public static readonly"),
        (r"\bstatic final\b", "static readonly"),
        (r"\bfinal\s+", ""),
        (r"\bextends\b", ":"),
        (r"\bimplements\b", ":"),
        (r"\binstanceof\b", "is"),
        (r"\bsuper\.", "base."),
    ]
    for a, b in reps:
        text = re.sub(a, b, text)

    # map.put(k, v) -> map[k] = v
    text = re.sub(r"\[__PUT__\(([^,]+),\s*(.+?)\);", r"[\1] = \2;", text)

    # for-each
    text = re.sub(r"\bfor\s*\(([\w<>\[\], .]+?)\s+(\w+)\s*:\s*([^)]+?)\)\s*\{", r"foreach (\1 \2 in \3) {", text)

    # readonly on methods / readonly static methods is invalid: "public readonly X foo(" -> "public X foo("
    text = re.sub(r"\b(public|private|protected) readonly ([\w<>\[\], ?.]+\s+\w+\s*\()", r"\1 \2", text)
    text = re.sub(r"\b(public|private|protected) static readonly ([\w<>\[\], ?.]+\s+\w+\s*\()", r"\1 static \2", text)

    # >>> unsigned shift
    text = re.sub(r"\(([^()]+?)\s*>>>\s*(\d+)\)", r"((int)((uint)(\1) >> \2))", text)
    text = re.sub(r"\(([^()]+)\)\s*>>>\s*0", r"(\1)", text)

    # int hex literals that overflow int
    def hexfix(m):
        v = int(m.group(1), 16)
        if v > 0x7FFFFFFF and not m.group(2):
            return f"unchecked((int){m.group(0)})"
        return m.group(0)

    text = re.sub(r"0[xX]([0-9a-fA-F]+)([lL]?)", hexfix, text)

    # @Override -> override (non-interface parents) / interface implementation
    text = re.sub(r"@Override\s+public\s+", "public override ", text)
    text = re.sub(r"@Override\s+protected\s+", "protected override ", text)
    text = re.sub(r"@Override\s+", "override ", text)

    body = text.strip("\n")
    using = (
        "// <auto-generated-from>material-color-utilities/java</auto-generated-from>\n"
        "// Translated to C# for M3E4Unity. Names follow the Java original on purpose.\n"
        "using System;\nusing System.Collections.Generic;\nusing System.Linq;\n"
    )
    common = [f"using SpecVersion = {NS}.ColorSpec.SpecVersion;", f"using Platform = {NS}.DynamicScheme.Platform;", f"using DeltaConstraint = {NS}.ToneDeltaPair.DeltaConstraint;"]
    aliases = sorted(set(aliases) | set(common))
    alias_block = "\n".join(aliases) + "\n\n"
    return f"{using}\nnamespace {NS}\n{{\n{alias_block}{body}\n}}\n"


def main():
    files = []
    for root, _, names in os.walk(SRC):
        for n in names:
            if n.endswith(".java"):
                files.append(os.path.join(root, n))
    texts = {f: open(f, encoding="utf-8").read() for f in files}
    for t in texts.values():
        collect_enums(t)
        INTERFACES.update(re.findall(r"\binterface\s+(\w+)", t))
    for f, t in texts.items():
        rel = os.path.relpath(f, SRC)
        out = os.path.join(DST, os.path.splitext(rel)[0] + ".cs")
        os.makedirs(os.path.dirname(out), exist_ok=True)
        with open(out, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(convert(t, os.path.basename(f)))
        print("wrote", out)


if __name__ == "__main__":
    main()
