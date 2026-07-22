using System.Collections;

namespace FinReconLab.Domain;

internal sealed class ValueReadOnlyList<T> : IReadOnlyList<T>, ICollection<T>, IEquatable<ValueReadOnlyList<T>>
{
    private readonly T[] items;

    public ValueReadOnlyList(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        this.items = items.ToArray();
    }

    public int Count => items.Length;

    public bool IsReadOnly => true;

    public T this[int index] => items[index];

    public bool Equals(ValueReadOnlyList<T>? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null || Count != other.Count)
        {
            return false;
        }

        var comparer = EqualityComparer<T>.Default;
        for (var index = 0; index < Count; index++)
        {
            if (!comparer.Equals(items[index], other.items[index]))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is ValueReadOnlyList<T> other && Equals(other);

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        foreach (var item in items)
        {
            hashCode.Add(item);
        }

        return hashCode.ToHashCode();
    }

    public bool Contains(T item) => Array.IndexOf(items, item) >= 0;

    public void CopyTo(T[] array, int arrayIndex) => items.CopyTo(array, arrayIndex);

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => items.GetEnumerator();

    public void Add(T item) => throw new NotSupportedException("Collection is read-only.");

    public void Clear() => throw new NotSupportedException("Collection is read-only.");

    public bool Remove(T item) => throw new NotSupportedException("Collection is read-only.");
}
