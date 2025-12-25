namespace AvRichTextBox;

internal sealed class InsertBlockEdit(FlowDocument doc, int index, Block block) : IAtomicEdit
{
   public void Apply() => doc.Blocks.Insert(index, block);
   public void Unapply() => doc.Blocks.Remove(block);
}


