namespace AvRichTextBox;

internal sealed class RevertAtomicEdit(IAtomicEdit inner) : IAtomicEdit
{
   public void Apply() => inner.Unapply();
   public void Unapply() => inner.Apply();
}


