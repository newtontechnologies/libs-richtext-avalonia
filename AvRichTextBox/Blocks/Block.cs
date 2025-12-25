using Avalonia;
using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace AvRichTextBox;

public class Block : INotifyPropertyChanged
{
   public event PropertyChangedEventHandler? PropertyChanged;
   public void NotifyPropertyChanged([CallerMemberName] String propertyName = "") { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)); }

   private Thickness _margin = new (0, 0, 0, 0);
   public Thickness Margin { get => _margin; set { _margin = value; NotifyPropertyChanged(nameof(Margin)); } }

   public string Text
   {
      get
      {
         string returnText = "";

         switch (this.GetType())
         {
            case Type t when t == typeof(Paragraph):
               returnText = string.Join("", ((Paragraph)this).Inlines.ToList().ConvertAll(ied => ied.InlineText));
               break;
               //case Type t when t == typeof(Table):
               //   returnText = "$";
               //   break;
         }
         return returnText;
      }
   }

   public bool IsParagraph => this.GetType() == typeof(Paragraph);
   //public bool IsTable => this.GetType() == typeof(Table);

   internal int SelectionLength => SelectionEndInBlock - SelectionStartInBlock;
   public int BlockLength => this.IsParagraph ? ((Paragraph)this).Inlines.ToList().Sum(il => il.InlineLength) + 1 : 1;  //Add one for paragraph itself

   private int _startInDoc = 0;
   internal int StartInDoc
   {
      get => _startInDoc;
      set { if (_startInDoc != value) { _startInDoc = value; NotifyPropertyChanged(nameof(StartInDoc)); } }
   }

   internal int EndInDoc => StartInDoc + BlockLength;

   private int _selectionStartInBlock;
   public int SelectionStartInBlock
   {
      get => _selectionStartInBlock;
      set { if (_selectionStartInBlock != value) { _selectionStartInBlock = value; NotifyPropertyChanged(nameof(SelectionStartInBlock)); } }
   }

   private int _selectionEndInBlock;
   public int SelectionEndInBlock
   {
      get => _selectionEndInBlock;
      set
      {

         if (_selectionEndInBlock != value)
         {
            _selectionEndInBlock = value; // Set the correct value
            NotifyPropertyChanged(nameof(SelectionEndInBlock));
         }
      }
   }

   internal void ClearSelection()
   {
      this.SelectionStartInBlock = 0;
      this.SelectionEndInBlock = 0;
      if (this is Paragraph p)
      {
         foreach (EditableInlineUiContainer iuc in p.Inlines.OfType<EditableInlineUiContainer>())
            iuc.IsSelected = false;
      }
   }

   internal void CollapseToStart() { if (SelectionStartInBlock != SelectionEndInBlock) SelectionEndInBlock = SelectionStartInBlock; }
   internal void CollapseToEnd() { if (SelectionStartInBlock != SelectionEndInBlock) SelectionStartInBlock = SelectionEndInBlock; }
}
