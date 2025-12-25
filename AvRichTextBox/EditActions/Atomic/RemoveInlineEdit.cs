namespace AvRichTextBox;

internal sealed class RemoveInlineEdit(Paragraph paragraph, int index, IEditable inline) : IAtomicEdit
{
   public void Apply() => paragraph.Inlines.Remove(inline);
   public void Unapply() => paragraph.Inlines.Insert(index, inline);
}


