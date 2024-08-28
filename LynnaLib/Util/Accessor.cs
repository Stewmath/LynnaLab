using System;
using System.Linq.Expressions;

namespace Util;

/// <summary>
/// Helper class allowing one to effectively pass a property as a reference. See ImGuiX.Checkbox
/// function for usage example.
///
/// From: https://stackoverflow.com/questions/1402803/passing-properties-by-reference-in-c-sharp
///
/// I'm unsure about the performance of this. It involves compiling some IL code (but not much).
/// </summary>
public class Accessor<T>
{
    public Accessor(Expression<Func<T>> expression)
    {
        if (expression.Body is not MemberExpression memberExpression)
            throw new ArgumentException("expression must return a field or property");
        var parameterExpression = Expression.Parameter(typeof(T));

        _setter = Expression.Lambda<Action<T>>(
            Expression.Assign(memberExpression, parameterExpression), parameterExpression).Compile();
        _getter = expression.Compile();
    }

    public void Set(T value) => _setter(value);
    public T Get() => _getter();

    private readonly Action<T> _setter;
    private readonly Func<T> _getter;
}
