using System;

namespace AvRichTextBox;

/// <summary>
/// Holds a reference to the most recent undo record (head) and enforces linear history.
/// The chain relationship is carried by the undo record itself via <see cref="Undo.Previous"/>.
/// </summary>
internal sealed class UndoStack
{
   private Undo? _head;

   internal int Count { get; private set; }

   internal void Clear()
   {
      _head = null;
      Count = 0;
   }

   internal void Add(Undo undo)
   {
      if (!ReferenceEquals(undo.Previous, _head))
         throw new InvalidOperationException("Undo record must reference the current head as its Previous.");

      _head = undo;
      Count++;
   }

   public Undo? Head => _head;

   internal Undo Last() => Head ?? throw new InvalidOperationException("The undo stack is empty.");

   internal void RemoveAt(int index)
   {
      if (index != Count - 1)
         throw new NotSupportedException("UndoStack only supports removing the most recent entry.");
      if (_head is null) throw new InvalidOperationException("The undo stack is empty.");

      _head = _head.Previous;
      Count--;
   }
}


