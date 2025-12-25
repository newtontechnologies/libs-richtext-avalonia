namespace AvRichTextBox;

internal sealed class RemoveBlockEdit(FlowDocument doc, int index, Block block) : IAtomicEdit
{
   public void Apply() => doc.Blocks.Remove(block);
   public void Unapply() => doc.Blocks.Insert(index, block);
}


