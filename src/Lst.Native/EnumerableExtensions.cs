namespace Lst.Native;

public static class EnumerableExtensions
{
  public static IEnumerable<(T, T)> Pairwise<T>(this IEnumerable<T> source)
  {
    using var enumerator = source.GetEnumerator();
    if (!enumerator.MoveNext())
      yield break;

    var previous = enumerator.Current;
    while (enumerator.MoveNext())
    {
      yield return (previous, enumerator.Current);
      previous = enumerator.Current;
    }
  }
}