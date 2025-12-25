using Avalonia.Controls.Documents;

namespace AvRichTextBox;

public partial class EditableParagraph
{

   private InlineCollection GetFormattedInlines()
   {
      InlineCollection returnInlines = [];
      foreach (IEditable ied in ((Paragraph)this.DataContext!).Inlines)
         returnInlines.Add(ied.BaseInline);

      return returnInlines;
   }
}

