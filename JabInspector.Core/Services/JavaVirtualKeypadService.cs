using JabInspector.Core.Models;

namespace JabInspector.Core.Services;

public sealed class JavaVirtualKeypadService
{
    public bool ShouldUseVirtualKeypad(AccessibleNode node, string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        if (node.AccessibleText || node.AccessibleValue) return false;
        if (node.Children.Count == 0) return false;

        var role = $"{node.Role} {node.RoleEnUs}";
        var looksLikeContainer =
            role.Contains("layered pane", StringComparison.OrdinalIgnoreCase) ||
            role.Contains("root pane", StringComparison.OrdinalIgnoreCase) ||
            role.Contains("panel", StringComparison.OrdinalIgnoreCase) ||
            role.Contains("pane", StringComparison.OrdinalIgnoreCase) ||
            role.Contains("keyboard", StringComparison.OrdinalIgnoreCase) ||
            node.ChildrenCount > 4;
        if (!looksLikeContainer) return false;

        var keyLikeDescendants = EnumerateDescendants(node)
            .Where(candidate => !ReferenceEquals(candidate, node))
            .Count(IsLikelyVirtualKey);
        if (keyLikeDescendants >= 3) return true;

        return text.Any(ch => !char.IsControl(ch) && FindVirtualKey(node, ch) is not null);
    }

    public bool TryBuildPlan(AccessibleNode keyboardRoot, string text, out VirtualKeypadPlan plan, out string message)
    {
        var steps = new List<VirtualKeypadStep>();
        foreach (var ch in text)
        {
            if (ch == '\r') continue;

            var key = FindVirtualKey(keyboardRoot, ch);
            if (key is null)
            {
                var label = GetCharacterLabel(ch);
                plan = new VirtualKeypadPlan(keyboardRoot, text, steps);
                message = $"Could not find virtual keypad key '{label}' under {keyboardRoot.DisplayName}. Matched {steps.Count} key(s) before stopping.";
                return false;
            }

            steps.Add(new VirtualKeypadStep(ch, GetCharacterLabel(ch), key));
        }

        plan = new VirtualKeypadPlan(keyboardRoot, text, steps);
        message = $"Resolved {steps.Count} virtual keypad key(s) under {keyboardRoot.DisplayName}.";
        return true;
    }

    public AccessibleNode? FindVirtualKey(AccessibleNode keyboardRoot, char ch)
    {
        var alternatives = BuildVirtualKeyAlternatives(ch)
            .Select(NormalizeKeyLabel)
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (alternatives.Count == 0) return null;

        var candidates = EnumerateDescendants(keyboardRoot)
            .Where(candidate => !ReferenceEquals(candidate, keyboardRoot))
            .Select(candidate => new
            {
                Node = candidate,
                Labels = GetVirtualKeyLabels(candidate).Select(NormalizeKeyLabel).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            })
            .Where(candidate => candidate.Labels.Count > 0)
            .ToList();

        return candidates.FirstOrDefault(candidate => candidate.Labels.Any(label => alternatives.Contains(label, StringComparer.OrdinalIgnoreCase)))?.Node
               ?? candidates.FirstOrDefault(candidate => candidate.Labels.Any(label => alternatives.Any(alt => IsLooseKeyMatch(label, alt))))?.Node;
    }

    public static IEnumerable<AccessibleNode> EnumerateDescendants(AccessibleNode node)
    {
        yield return node;
        foreach (var child in node.Children)
        {
            foreach (var nested in EnumerateDescendants(child))
                yield return nested;
        }
    }

    public static string GetCharacterLabel(char ch) => ch switch
    {
        '\n' => "Enter",
        ' ' => "Space",
        _ => ch.ToString()
    };

    private static bool IsLikelyVirtualKey(AccessibleNode node)
    {
        var labels = GetVirtualKeyLabels(node).Select(NormalizeKeyLabel).Where(x => x.Length > 0).ToList();
        if (labels.Count == 0) return false;
        if (labels.Any(label => label.Length == 1 || IsSpecialKeyLabel(label))) return true;

        var role = $"{node.Role} {node.RoleEnUs}";
        return labels.Any(label => label.Length <= 8)
               && (role.Contains("button", StringComparison.OrdinalIgnoreCase)
                   || role.Contains("label", StringComparison.OrdinalIgnoreCase)
                   || role.Contains("text", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> GetVirtualKeyLabels(AccessibleNode node)
    {
        if (!string.IsNullOrWhiteSpace(node.Name)) yield return node.Name;
        if (!string.IsNullOrWhiteSpace(node.VirtualAccessibleName)) yield return node.VirtualAccessibleName;
        if (!string.IsNullOrWhiteSpace(node.Description)) yield return node.Description;
        if (!string.IsNullOrWhiteSpace(node.TextPreview)) yield return node.TextPreview;
        if (!string.IsNullOrWhiteSpace(node.CurrentValue)) yield return node.CurrentValue;
    }

    private static IEnumerable<string> BuildVirtualKeyAlternatives(char ch)
    {
        if (ch == '\n')
        {
            yield return "enter";
            yield return "return";
            yield return "ok";
            yield return "done";
            yield break;
        }

        if (ch == ' ')
        {
            yield return "space";
            yield return "spacebar";
            yield return "blank";
            yield break;
        }

        yield return ch.ToString();
        yield return char.ToLowerInvariant(ch).ToString();
        yield return char.ToUpperInvariant(ch).ToString();

        if (char.IsDigit(ch))
        {
            yield return $"digit {ch}";
            yield return $"digit{ch}";
            yield return $"number {ch}";
            yield return $"number{ch}";
            yield return $"num {ch}";
            yield return $"num{ch}";
            yield return $"key {ch}";
            yield return $"key{ch}";
            yield break;
        }

        if (char.IsLetter(ch))
        {
            var lower = char.ToLowerInvariant(ch);
            yield return $"letter {lower}";
            yield return $"letter{lower}";
            yield return $"key {lower}";
            yield return $"key{lower}";
            yield break;
        }

        foreach (var label in ch switch
        {
            '.' => new[] { "dot", "period", "decimal", "point" },
            ',' => new[] { "comma" },
            '-' => new[] { "minus", "dash", "hyphen", "negative" },
            '+' => new[] { "plus", "add" },
            '/' => new[] { "slash", "divide" },
            '*' => new[] { "star", "asterisk", "multiply" },
            '#' => new[] { "hash", "pound" },
            '@' => new[] { "at" },
            ':' => new[] { "colon" },
            ';' => new[] { "semicolon" },
            _ => Array.Empty<string>()
        })
        {
            yield return label;
        }
    }

    private static string NormalizeKeyLabel(string value)
    {
        return new string(value
            .Trim()
            .ToLowerInvariant()
            .Where(ch => char.IsLetterOrDigit(ch) || ch is '.' or ',' or '-' or '+' or '/' or '*' or '#' or '@' or ':' or ';')
            .ToArray());
    }

    private static bool IsSpecialKeyLabel(string label) =>
        label is "enter" or "return" or "ok" or "done" or "space" or "spacebar" or "clear" or "delete" or "backspace" or "bksp";

    private static bool IsLooseKeyMatch(string label, string expected)
    {
        if (label.Length > expected.Length + 8) return false;
        return label.EndsWith(expected, StringComparison.OrdinalIgnoreCase)
               || label.StartsWith(expected, StringComparison.OrdinalIgnoreCase);
    }
}
