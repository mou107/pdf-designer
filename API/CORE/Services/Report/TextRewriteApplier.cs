using API.CORE.Schemas;
using Stimulsoft.Report;
using Stimulsoft.Report.Components;

namespace API.CORE.Services.Report
{
    /// <summary>
    /// Reecrit le contenu des textes du modele quand ils portent certains marqueurs.
    /// </summary>
    /// <remarks>
    /// C'est le moyen de reprendre la main sur un modele personnalise dans l'editeur avance : son .mrt
    /// fige des liaisons que l'appelant veut rediriger vers une valeur qu'il calcule lui-meme. Les regles
    /// sont evaluees dans l'ordre, la premiere qui correspond gagne, et une regle deja appliquee est
    /// ignoree (<c>skipIfContains</c>) — l'operation reste donc idempotente.
    /// </remarks>
    internal static class TextRewriteApplier
    {
        public static void Apply(StiReport report, IEnumerable<TextRewriteDirective>? directives)
        {
            var rules = (directives ?? Enumerable.Empty<TextRewriteDirective>())
                .Where(rule => !string.IsNullOrEmpty(rule.SetTo))
                .ToList();

            if (rules.Count == 0) return;

            foreach (var component in ReportPrimitives.AllComponents(report))
            {
                if (component is not StiText text) continue;

                if (text.Text == null) continue;

                var value = text.Text.Value;
                if (string.IsNullOrEmpty(value)) continue;

                foreach (var rule in rules)
                {
                    if (!Matches(value, rule)) continue;
                    text.Text.Value = rule.SetTo;
                    break;
                }
            }
        }

        private static bool Matches(string value, TextRewriteDirective rule)
        {
            if (rule.SkipIfContains != null && rule.SkipIfContains.Any(value.Contains))
                return false;

            var hasAny = rule.WhenContainsAny is { Count: > 0 };
            var hasAll = rule.WhenContainsAll is { Count: > 0 };
            if (!hasAny && !hasAll) return false;

            if (hasAny && !rule.WhenContainsAny!.Any(value.Contains)) return false;
            if (hasAll && !rule.WhenContainsAll!.All(value.Contains)) return false;

            return true;
        }
    }
}
