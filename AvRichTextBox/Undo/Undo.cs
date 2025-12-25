namespace AvRichTextBox;

public abstract class Undo
{
   protected Undo(Undo? previous)
   {
      Previous = previous;
   }

   internal Undo? Previous { get; }

   public abstract void PerformUndo();
   public abstract int UndoEditOffset { get; }
   public abstract bool UpdateTextRanges { get; }
}

internal class EditablePropertyAssociation
{
   internal IEditable InlineItem { get; set; }
   internal object PropertyValue { get; set; }
   internal FlowDocument.FormatRun? FormatRun { get; set; }  

   internal EditablePropertyAssociation(IEditable inlineItem, FlowDocument.FormatRun formatRun, object propertyValue)
   {
      InlineItem = inlineItem;
      FormatRun = formatRun;
      PropertyValue = propertyValue;
   }
}