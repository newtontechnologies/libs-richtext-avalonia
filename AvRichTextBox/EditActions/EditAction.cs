namespace AvRichTextBox;

/// <summary>
/// Represents a single reversible edit operation.
/// Apply = performs the edit (Redo direction).
/// Unapply = reverts the edit (Undo direction).
/// </summary>
internal abstract class EditAction
{
   internal abstract void Apply(FlowDocument doc);
   internal abstract void Unapply(FlowDocument doc);
}
