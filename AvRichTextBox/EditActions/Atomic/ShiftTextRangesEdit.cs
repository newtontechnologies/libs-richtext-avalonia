namespace AvRichTextBox;

internal sealed class ShiftTextRangesEdit(FlowDocument doc, int editStart, int delta) : IAtomicEdit
{
   public void Apply() => doc.UpdateTextRanges(editStart, delta);
   public void Unapply() => doc.UpdateTextRanges(editStart, -delta);
}


