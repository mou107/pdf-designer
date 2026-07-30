using API.CORE.Schemas;
using Stimulsoft.Report;

namespace API.CORE.Services.Report
{
    /// <summary>
    /// Applique les directives d'une requete a un rapport charge.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Le meme jeu de directives sert deux sinks : le rendu d'un document, et l'ouverture du designer
    /// web. Les avoir au meme endroit garantit que ce que l'utilisateur edite ressemble a ce qui sera
    /// imprime.
    /// </para>
    /// <para>
    /// L'ordre n'est pas arbitraire : les couleurs passent avant la mise en page (qui pose ses propres
    /// bordures), les retouches ponctuelles apres elle (elles ont le dernier mot sur un composant), et
    /// les conditions en tout dernier — elles repartent de la police effective des cellules.
    /// </para>
    /// </remarks>
    internal static class DirectiveApplier
    {
        /// <param name="includeImages">
        /// Poser les images (logo, cachet, filigrane, rangees d'images) ou non.
        /// <para>
        /// Le designer les laisse VOLONTAIREMENT de cote : ce qu'il affiche est ce qui sera fige dans le
        /// .mrt a l'enregistrement, et figer une image en base64 rendrait le modele insensible a un
        /// changement de logo — le piege qui avait deja fige le papier en-tete sur des copies societe.
        /// </para>
        /// </param>
        public static void Apply(StiReport report, RenderRequest request, bool includeImages)
        {
            ColorApplier.ApplyStyles(report, request.Styles);
            ColorApplier.ApplyReplacements(report, request.ColorReplacements);
            BandLayoutApplier.Apply(report, request.Bands);
            TextRewriteApplier.Apply(report, request.TextRewrites);

            if (includeImages)
            {
                ImageApplier.ApplyImages(report, request.Images);
                ImageApplier.ApplyWatermark(report, request.Watermark);
                ImageApplier.ApplyImageRows(report, request.ImageRows);
            }

            ComponentOverrideApplier.Apply(report, request.ComponentOverrides);
            ComponentOverrideApplier.ApplyRowFills(report, request.RowFills);
            ConditionsApplier.Apply(report, request.Conditions);
        }
    }
}
