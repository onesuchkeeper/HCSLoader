using System;
using System.Collections.Generic;

namespace HCSLoader
{
	public static class Extensions
	{
		public static int? FindNthIndex<T>(this IEnumerable<T> enumerable, int count, Func<T, bool> predicate)
		{
			if (count < 1)
				throw new ArgumentException("Count must be at least 1.", nameof(count));

			int counter = 0;
			int index = 0;

			foreach (var t in enumerable)
			{
				if (predicate(t))
					counter++;

				if (counter >= count)
					return index;

				index++;
			}

			return null;
		}
	}
}