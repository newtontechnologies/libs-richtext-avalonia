using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AvRichTextBox;

public class EditableInlineUiContainer : InlineUIContainer, IEditable, INotifyPropertyChanged
{
   public new event PropertyChangedEventHandler? PropertyChanged;
   private void NotifyPropertyChanged([CallerMemberName] string propertyName = "") { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)); }

   public EditableInlineUiContainer(Control c) { Child = c; }

   public Inline BaseInline => this;
   public Paragraph? MyParagraph { get; set; }
   public int TextPositionOfInlineInParagraph { get; set; }
   public string InlineText { get; set; } = "@";
   public string DisplayInlineText { get => "<UICONTAINER> => " + (this.Child != null && this.Child.GetType() == typeof(Image) ? "Image" : "NoChild"); }
   public string FontName => "---";
   public int InlineLength => 1;
   public bool IsEmpty => false;
   public bool IsLastInlineOfParagraph { get; set; }
   //public double InlineHeight => (this.Child != null && this.Child.GetType() == typeof(Image) ? : this.Child.Bounds.Height;
   public double InlineHeight => Child == null ? 0 : this.Child.Bounds.Height;
   

   public int ImageNo;

   public IEditable Clone() { return new EditableInlineUiContainer(this.Child){ MyParagraph = this.MyParagraph }; }

   //for DebuggerPanel 
   private bool _isStartInline = false;
   public bool IsStartInline { get => _isStartInline; set { _isStartInline = value; NotifyPropertyChanged(nameof(BackBrush)); NotifyPropertyChanged(nameof(InlineSelectedBorderThickness)); } }
   private bool _isEndInline = false;
   public bool IsEndInline { get => _isEndInline; set { _isEndInline = value; NotifyPropertyChanged(nameof(BackBrush)); } }
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

   private bool _isSelected = false;
   public bool IsSelected { get => _isSelected; set { _isSelected = value; this.Child.Opacity = value ? 0.2 : 1; } }

}


