using System.Text.Json;

namespace API.CORE.Schemas
{
    /// <summary>
    /// Le contrat de rendu : un modele, des donnees, et des directives qui decrivent quoi appliquer
    /// dessus. Le moteur n'interprete AUCUN nom metier — tout ce qu'il manipule (composants, bandes,
    /// colonnes, couleurs) est nomme par l'appelant, qui est le seul a connaitre son propre .mrt.
    /// </summary>
    /// <remarks>
    /// C'est ce qui rend le service reutilisable par n'importe quel projet : deux applications aux
    /// modeles de donnees totalement differents envoient le meme genre de requete, seuls les noms
    /// changent.
    /// </remarks>
    public class RenderRequest
    {
        public int SchemaVersion { get; set; } = 2;

        /// <summary>Contenu XML du modele Stimulsoft (.mrt). REQUIS.</summary>
        public string? Mrt { get; set; }

        /// <summary>
        /// Donnees du document, de forme LIBRE : elles sont enregistrees telles quelles comme source du
        /// rapport. Les expressions du .mrt decident de ce qui est lu.
        /// </summary>
        public JsonElement? Data { get; set; }

        /// <summary>Nom du jeu de donnees enregistre dans le rapport (defaut <c>data</c>).</summary>
        public string? DataSetName { get; set; }

        /// <summary>Nom du fichier produit, sans extension (defaut <c>document</c>).</summary>
        public string? FileName { get; set; }

        /// <summary>Images a poser sur des composants du modele, designes par leur nom.</summary>
        public List<ImageDirective>? Images { get; set; }

        /// <summary>Filigrane pleine page. Absent ou vide = le modele garde le sien.</summary>
        public WatermarkDirective? Watermark { get; set; }

        /// <summary>
        /// Rangees d'images AJOUTEES au modele (pas de composant a alimenter). Permet de poser une serie
        /// d'images que le .mrt ne prevoit pas, y compris sur un modele personnalise.
        /// </summary>
        public List<ImageRowDirective>? ImageRows { get; set; }

        /// <summary>Remplacements de couleur appliques a tout le rapport (fonds, textes, bordures).</summary>
        public List<ColorReplacement>? ColorReplacements { get; set; }

        /// <summary>Styles nommes crees ou mis a jour, referencables par <see cref="BandDirective"/>.</summary>
        public List<StyleDirective>? Styles { get; set; }

        /// <summary>Mise en page des bandes : colonnes visibles, largeurs, hauteur de ligne, bordures.</summary>
        public List<BandDirective>? Bands { get; set; }

        /// <summary>Retouches ponctuelles d'un composant designe par son nom.</summary>
        public List<ComponentOverride>? ComponentOverrides { get; set; }

        /// <summary>Coloration d'une rangee reperee par l'expression qu'elle affiche.</summary>
        public List<RowFillDirective>? RowFills { get; set; }

        /// <summary>
        /// Reecriture d'expressions dans les textes du modele. Sert a reprendre la main sur un .mrt
        /// personnalise, dont les liaisons ont ete figees par l'utilisateur dans le designer.
        /// </summary>
        public List<TextRewriteDirective>? TextRewrites { get; set; }

        /// <summary>Conditions Stimulsoft : mise en forme evaluee ligne par ligne dans une bande.</summary>
        public List<ConditionDirective>? Conditions { get; set; }

        /// <summary>
        /// Documents (PDF ou image, en base64) fusionnes A LA FIN du rendu. Sans effet en export image.
        /// </summary>
        public List<string>? Attachments { get; set; }
    }

    /// <summary>Image posee sur un composant image du modele.</summary>
    public class ImageDirective
    {
        /// <summary>Nom du composant image dans le .mrt. REQUIS.</summary>
        public string? Component { get; set; }

        /// <summary>Contenu base64 (prefixe <c>data:</c> accepte). null/vide efface l'image du modele.</summary>
        public string? Base64 { get; set; }

        /// <summary>Redimensionne la boite du composant (pixels CSS 96 dpi). 0 ou absent = inchange.</summary>
        public double? WidthPx { get; set; }
        public double? HeightPx { get; set; }
    }

    /// <summary>Filigrane pleine page.</summary>
    public class WatermarkDirective
    {
        public string? Base64 { get; set; }
        public bool Stretch { get; set; } = true;

        /// <summary>0 = opaque, 100 = invisible.</summary>
        public int Transparency { get; set; }
    }

    /// <summary>
    /// Rangee d'images creee au rendu et ajoutee dans une bande, alignee de gauche a droite.
    /// </summary>
    public class ImageRowDirective
    {
        /// <summary>
        /// Bande d'accueil. Absent = la premiere bande de pied de page, creee si le modele n'en a pas —
        /// ce qui rend la rangee independante de la structure du modele.
        /// </summary>
        public string? Band { get; set; }

        /// <summary>Images en base64 (data URL acceptee). Les entrees illisibles sont ignorees.</summary>
        public List<string>? Images { get; set; }

        /// <summary>Nombre maximum d'images posees (0 = pas de limite).</summary>
        public int MaxCount { get; set; }

        public double SizeCm { get; set; } = 2.0;
        public double GapCm { get; set; } = 0.3;
        public double MarginTopCm { get; set; } = 0.3;

        /// <summary>Prefixe des noms donnes aux composants crees (suffixes 1, 2, 3…).</summary>
        public string? NamePrefix { get; set; }
    }

    /// <summary>
    /// Remplace une couleur par une autre partout dans le rapport. La tolerance absorbe les ecarts
    /// d'arrondi entre l'editeur et le fichier.
    /// </summary>
    public class ColorReplacement
    {
        public string? From { get; set; }
        public string? To { get; set; }
        public int Tolerance { get; set; } = 6;
    }

    /// <summary>Style nomme portant un fond — reference par <c>oddStyle</c> / <c>evenStyle</c>.</summary>
    public class StyleDirective
    {
        public string? Name { get; set; }
        public string? BackColor { get; set; }
    }

    /// <summary>Mise en page d'une bande de donnees et de son entete.</summary>
    public class BandDirective
    {
        /// <summary>Nom de la bande de donnees. REQUIS.</summary>
        public string? Name { get; set; }

        /// <summary>Nom de la bande d'entete correspondante, si le reflow des colonnes est demande.</summary>
        public string? HeaderBand { get; set; }

        /// <summary>Prefixes des noms de composants qui identifient une colonne (ex. HCol_ / DCol_).</summary>
        public ColumnPrefixes? ColumnPrefixes { get; set; }

        /// <summary>
        /// Colonnes DANS L'ORDRE d'affichage, de gauche a droite. Vide ou absent = la mise en page du
        /// modele est conservee telle quelle.
        /// </summary>
        public List<ColumnDirective>? Columns { get; set; }

        /// <summary>Largeur utile totale de la ligne, en centimetres — sert a repartir les colonnes.</summary>
        public double? TotalWidthCm { get; set; }

        /// <summary>Cle de la colonne qui absorbe la largeur restante.</summary>
        public string? FlexColumn { get; set; }

        /// <summary>Largeur plancher de la colonne flexible.</summary>
        public double FlexMinWidthCm { get; set; } = 1.0;

        /// <summary>Hauteur des lignes de la bande. Absent = hauteur du modele.</summary>
        public double? RowHeightCm { get; set; }

        /// <summary>Styles nommes appliques aux lignes impaires / paires (voir <see cref="StyleDirective"/>).</summary>
        public string? OddStyle { get; set; }
        public string? EvenStyle { get; set; }

        /// <summary>Bordure posee sur les cellules de la bande de donnees.</summary>
        public BorderDirective? Border { get; set; }

        /// <summary>Bordure posee sur les cellules de la bande d'entete.</summary>
        public BorderDirective? HeaderBorder { get; set; }

        /// <summary>Composants de la bande exclus de <see cref="Border"/> (fonds de ligne…).</summary>
        public List<string>? BorderExclude { get; set; }
    }

    /// <summary>Prefixes qui relient une cellule d'entete a sa cellule de donnees par la meme cle.</summary>
    public class ColumnPrefixes
    {
        public string? Header { get; set; }
        public string? Data { get; set; }
    }

    /// <summary>Une colonne du tableau, identifiee par la cle qui suit le prefixe dans le nom du composant.</summary>
    public class ColumnDirective
    {
        public string? Key { get; set; }
        public bool Visible { get; set; } = true;

        /// <summary>Largeur imposee (cm). Absent = largeur du modele.</summary>
        public double? WidthCm { get; set; }
    }

    /// <summary>Bordure : cotes, couleur, epaisseur.</summary>
    public class BorderDirective
    {
        /// <summary>
        /// Liste separee par des virgules : <c>none</c>, <c>all</c>, <c>top</c>, <c>bottom</c>,
        /// <c>left</c>, <c>right</c>.
        /// </summary>
        public string? Sides { get; set; }

        public string? Color { get; set; }
        public double Width { get; set; } = 1;
    }

    /// <summary>Retouche d'un composant designe par son nom.</summary>
    public class ComponentOverride
    {
        public string? Name { get; set; }

        /// <summary>Fond. <c>transparent</c> est accepte.</summary>
        public string? BackColor { get; set; }

        public string? TextColor { get; set; }
        public double? HeightCm { get; set; }
        public double? WidthCm { get; set; }
        public bool? Enabled { get; set; }

        /// <summary>Vide les conditions posees par le modele sur ce composant.</summary>
        public bool ClearConditions { get; set; }
    }

    /// <summary>
    /// Colorie la rangee d'une bande reperee par l'expression qu'elle affiche : tous les textes places
    /// a la meme hauteur que le composant portant <see cref="AnchorExpression"/> prennent le fond donne.
    /// </summary>
    public class RowFillDirective
    {
        /// <summary>Nom (ou debut de nom) de la bande contenant la rangee.</summary>
        public string? Band { get; set; }

        /// <summary>Fragment d'expression qui identifie la rangee (ex. <c>totaux.totalHT</c>).</summary>
        public string? AnchorExpression { get; set; }

        public string? BackColor { get; set; }

        /// <summary>Ne colorie que les composants a droite de cette abscisse (cm).</summary>
        public double MinLeftCm { get; set; }

        /// <summary>Ecart vertical tolere pour considerer deux composants sur la meme rangee (cm).</summary>
        public double ToleranceCm { get; set; } = 0.05;
    }

    /// <summary>
    /// Remplace le contenu d'un texte du modele quand il porte certains marqueurs. Les regles sont
    /// evaluees dans l'ordre ; la premiere qui correspond gagne.
    /// </summary>
    public class TextRewriteDirective
    {
        /// <summary>Il suffit qu'UN de ces fragments soit present.</summary>
        public List<string>? WhenContainsAny { get; set; }

        /// <summary>TOUS ces fragments doivent etre presents.</summary>
        public List<string>? WhenContainsAll { get; set; }

        /// <summary>La regle est ignoree si l'un de ces fragments est present (idempotence).</summary>
        public List<string>? SkipIfContains { get; set; }

        /// <summary>Nouveau contenu du texte.</summary>
        public string? SetTo { get; set; }
    }

    /// <summary>
    /// Condition Stimulsoft : mise en forme evaluee ligne par ligne, seul moyen de styler differemment
    /// les lignes rendues par les memes composants.
    /// </summary>
    public class ConditionDirective
    {
        /// <summary>Bande dont les cellules recoivent la condition. REQUIS.</summary>
        public string? Band { get; set; }

        /// <summary>Restreint aux composants nommes. Vide = toute la bande.</summary>
        public List<string>? Components { get; set; }

        /// <summary>Composants exclus (fond de ligne, cellule deja stylee autrement…).</summary>
        public List<string>? ExcludeComponents { get; set; }

        /// <summary>Expression Stimulsoft evaluee par ligne (ex. <c>lignes.type == "article"</c>).</summary>
        public string? Expression { get; set; }

        public double? FontSize { get; set; }
        public bool Bold { get; set; }
        public bool Italic { get; set; }
        public bool Underline { get; set; }

        /// <summary>Couleur du texte. Absent = celle du composant est conservee.</summary>
        public string? TextColor { get; set; }
    }
}
