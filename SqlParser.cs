using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using Moq;
using Moq.Language.Flow;

namespace SqlParseTree
{
    public class Property
    {
        public Property(string? name, object? value)
        {
            Name = name;
            Value = value;
        }
        public readonly string? Name;
        public readonly object? Value;
    }

    public class ParseData
    {
        public ParseData(string typeName, TSqlFragment fragment)
        {
            TypeName = typeName;
            m_fragment = fragment;
            Text = fragment.AsText();
        }

        public void AddChild(ParseData data) => (Children ??= new()).Add(data);

        public int Count() => 1 + (Children?.Sum(x => x.Count()) ?? 0);

        public void PopulateProperties()
        {
            var visited = new HashSet<object>();
            GetFragments(visited);
            PopulateProperties(visited);
        }

        private void GetFragments(HashSet<object> fragments)
        {
            fragments.Add(m_fragment);
            if (Children != default)
            {
                foreach (var child in Children)
                {
                    child.GetFragments(fragments);
                }
            }
        }

        private void PopulateProperties(HashSet<object> visited)
        {
            Properties = GetProperties(m_fragment, visited);
            if (Children != default)
            {
                foreach (var child in Children)
                {
                    child.PopulateProperties(visited);
                }
            }
        }

        public readonly string TypeName;
        public readonly string Text;
        public List<ParseData>? Children;

        public List<Property>? Properties;
        private readonly TSqlFragment m_fragment;

        private static List<Property>? GetProperties(object obj, HashSet<object> visited)
        {
            List<Property>? result = default;
            foreach (var field in obj.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                processes(field, field.GetValue(obj));
            }
            foreach (var property in obj.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (property.GetIndexParameters().Length == 0)
                {
                    processes(property, property.GetValue(obj));
                }
            }
            return result?.OrderBy(x => x.Name).ToList();

            void processes(MemberInfo member, object? value)
            {
                if (member.DeclaringType != typeof(TSqlFragment))
                {
                    var convertedItem = convert(value);
                    if (convertedItem != default)
                    {
                        (result ??= new ()).Add(new (member.Name, convertedItem));
                    }
                }
            }

            object? convert(object? value)
            {
                if (value == default || visited.Contains(value))
                {
                    return default;
                }

                var t = value.GetType();
                if (t == typeof(string) || t.IsValueType)
                {
                    return value.ToString();
                }

                if (value is IEnumerable enumerable)
                {
                    List<Property>? items = default;
                    foreach (var item in enumerable)
                    {
                        var convertedItem = convert(item);
                        if (convertedItem != default)
                        {
                            (items ??= new ()).Add(new (default, convertedItem));
                        }
                    }
                    return items;
                }

                visited.Add(value);
                return GetProperties(value, visited);
            }
        }
    }

    // TODO, using a Mock like this makes it very slow...
    public class SqlParser : Mock<TSqlFragmentVisitor>
    {
        public static ParseData Parse(TSqlFragment content, StringBuilder log)
        {
            var watch = Stopwatch.StartNew();
            var parser = new SqlParser(log);
            log.AppendLine($"Create Sql Parser time: {watch.Elapsed}");
            watch.Restart();
            var visitor = parser.Object;
            log.AppendLine($"Create Visitor time: {watch.Elapsed}");
            watch.Restart();
            content.Accept(visitor);
            log.AppendLine($"Visitor time: {watch.Elapsed}");
            watch.Restart();
            parser.m_data!.PopulateProperties();
            log.AppendLine($"post time: {watch.Elapsed}");
            return parser.m_data;
        }

        private SqlParser(StringBuilder log)
        {
            m_log = log;
            CallBase = true;

            var isAny = typeof(It).GetMethods(BindingFlags.Static | BindingFlags.Public).Single(x => x.Name == nameof(It.IsAny));
            var addCallBack = typeof(SqlParser).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic).Single(x => x.Name == nameof(AddCallback));

            // The following is equivalent to (TSqlFragmentVisitor x)
            var parameter = Expression.Parameter(typeof(TSqlFragmentVisitor), "x");

            foreach (var method in typeof(TSqlFragmentVisitor).GetMethods().Where(x => x.Name == nameof(TSqlFragmentVisitor.ExplicitVisit) && x.GetParameters().Length == 1))
            {
                var parameterType = method.GetParameters().Single().ParameterType;

                // The following is equivalent to x => x.ExplicitVisit(It.IsAny<{parameterType}>())
                var explicitVisitCall = Expression.Call(parameter, method, Expression.Call(instance: default, isAny.MakeGenericMethod(parameterType)));
                var setupLambda = Expression.Lambda(delegateType: typeof(Action<TSqlFragmentVisitor>), explicitVisitCall, parameter);

                // so this is equivalent to mock.Setup(x => x.ExplicitVisit<{parameterType}>(It.IsAny<{parameterType}>())
                var setup = (ISetup<TSqlFragmentVisitor>)GetType()
                    .GetMethod(nameof(Mock<TSqlFragmentVisitor>.Setup), types: new [] { setupLambda.GetType()})
                    !.Invoke(this, new object [] { setupLambda })!;

                // this will add on the equivalent to setup.Callback(({parameterType} n) => Callback<{parameterType}>(method, n))
                // we can't use what we did to .Setup b/c we need a closure to pass method.
                addCallBack.MakeGenericMethod(parameterType).Invoke(this, new object[] {method, setup});
            }
        }

        private void Callback<T>(MethodInfo method, T node) where T : TSqlFragment
        {
            var parseData = new ParseData(typeof(T).Name, node);
            m_data ??= parseData;

            if (m_stack.Any())
            {
                m_stack.Peek().AddChild(parseData);
            }
            m_stack.Push(parseData);

            // Now call the base class's implantation
            var ftnPtr = method.MethodHandle.GetFunctionPointer();
            var action = (Action<T>)Activator.CreateInstance(typeof(Action<T>), Object, ftnPtr)!;
            action(node);

            m_stack.Pop();
        }

        private void AddCallback<T>(MethodInfo method, ISetup<TSqlFragmentVisitor> setup) where T : TSqlFragment
            => setup.Callback((T n) => Callback(method, n));

        private readonly StringBuilder m_log;

        private ParseData? m_data;
        private readonly Stack<ParseData> m_stack = new ();
    }
}
