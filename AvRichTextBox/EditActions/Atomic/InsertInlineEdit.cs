namespace AvRichTextBox;

internal sealed class InsertInlineEdit(Paragraph paragraph, int index, IEditable inline) : IAtomicEdit
{
   public void Apply() => paragraph.Inlines.Insert(index, inline);
   public void Unapply() => paragraph.Inlines.Remove(inline);
}


