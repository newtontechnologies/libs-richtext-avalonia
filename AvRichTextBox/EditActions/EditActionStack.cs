using System.Collections.Generic;

namespace AvRichTextBox;

internal sealed class EditActionStack
{
   private readonly Stack<CompositeEditAction> _stack = new();

   internal int Count => _stack.Count;
   internal bool Any => _stack.Count > 0;

   internal void Clear() => _stack.Clear();

   internal void Push(CompositeEditAction action) => _stack.Push(action);

   internal CompositeEditAction Pop() => _stack.Pop();

   internal CompositeEditAction Peek() => _stack.Peek();
   
   internal bool TryPop(out CompositeEditAction? action)
   {
      if (_stack.Count == 0)
      {
         action = null;
         return false;
      }

      action = _stack.Pop();
      return true;
   }
}


