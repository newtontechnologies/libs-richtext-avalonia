using Avalonia;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AvRichTextBox;

public class EditableLineBreak : LineBreak, IEditable, INotifyPropertyChanged
{
   public new event PropertyChangedEventHandler? PropertyChanged;
   private void NotifyPropertyChanged([CallerMemberName] string propertyName = "") { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)); }

   public EditableLineBreak() { }

   public Inline BaseInline => this;
   public Paragraph? MyParagraph { get; set; }
   public int TextPositionOfInlineInParagraph { get; set; }
   public string InlineText { get; set; } = @"\v"; //make literal to count as 2 characters
   //public string InlineText { get; set; } = "\v";
   public string DisplayInlineText => "{>LINEBREAK<}";
   public int InlineLength => 2;  //because LineBreak acts as a double character in TextBlock? - anyway don't use LineBreak, use \v instead
   public double InlineHeight => FontSize;
   public string FontName => "---";
   public bool IsEmpty => false;
   public bool IsLastInlineOfParagraph { get; set; }
 
   public IEditable Clone() => new EditableLineBreak() { MyParagraph = this.MyParagraph };


   //for DebuggerPanel 
   private bool _isStartInline = false;
   public bool IsStartInline { get => _isStartInline; set { _isStartInline = value; NotifyPropertyChanged(nameof(BackBrush)); NotifyPropertyChanged(nameof(InlineSelectedBorderThickness)); } }
   private bool _isEndInline = false;
   public bool IsEndInline { get => _isEndInline; set { _isEndInline = value; NotifyPropertyChanged(nameof(BackBrush)); NotifyPropertyChanged(nameof(InlineSelectedBorderThickness)); } }
   private bool _isWithinSelectionInline = false;
   public bool IsWithinSelectionInline { get => _isWithinSelectionInline; set { _isWithinSelectionInline = value; NotifyPropertyChanged(nameof(BackBrush)); } }
   public Thickness InlineSelectedBorderThickness => (IsStartInline || IsEndInline) ? new Thickness(3) : new Thickness(1);

   public SolidColorBrush BackBrush
   {
      get
      {
         if (IsStartInline) return new SolidColorBrush(Colors.LawnGreen);
         else if (IsEndInline) return new SolidColorBrush(Colors.Pink);
         else if (IsWithinSelectionInline) return new SolidColorBrush(Colors.LightGray);
         else return new SolidColorBrush(Colors.Transparent);
      }
   }

   public string InlineToolTip => "";

   public int CursorSpanLength => 1;

}

