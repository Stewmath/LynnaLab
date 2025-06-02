using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LynnaLib;

/// <summary>
/// Represents a ProjectDataType as an id uniquely representing that object. This reference can be
/// easily serialized using that id. When the value itself is required, it can be obtained with an
/// implicit conversion, but only once it is available (ie. it may not be available from the network
/// yet).
///
/// The idea is that any objects with trackable state should not be referencing most class instances
/// directly, because we can't really serialize a reference like that. Use this instead to track
/// references to those classes using IDs.
///
/// Two InstanceResolvers are considered equal if they reference the same data.
/// </summary>
public class InstanceResolver<T> where T : ProjectDataType
{
    /// <summary>
    /// Construct with only an identifier (the object itself might not be instantiated yet).
    ///
    /// If typeName is not equal to T, it should be a derived class.
    /// </summary>
    public InstanceResolver(Project project, string typeName, string id, bool resolveImplicitly = true)
    {
        Helper.Assert(project != null);
        this.project = project;
        this.resolveImplicitly = resolveImplicitly;
        this.InstanceType = Project.GetInstType(typeName);
        this.Identifier = id;
        this.ResolvedValue = false;

        if (!(InstanceType == typeof(T) || typeof(T).IsAssignableFrom(InstanceType)))
            throw new Exception($"InstanceResolver type mismatch: {typeof(T).Name} not assignable from {InstanceType.Name}");
    }

    /// <summary>
    /// Construct with an existing instance of something
    /// </summary>
    public InstanceResolver(T instance, bool resolveImplicitly = true)
    {
        this.project = instance.Project;
        this.resolveImplicitly = resolveImplicitly;
        this.instance = instance;
        this.InstanceType = instance.GetType();
        this.Identifier = instance.Identifier;
        this.ResolvedValue = true;

        if (!project.CheckHasDataType(InstanceType, Identifier))
            throw new Exception("InstanceResolver: couldn't find " + FullIdentifier);
    }

    T instance;
    Project project;
    bool resolveImplicitly;

    /// <summary>
    /// Accessing this "resolves" the instance if it hasn't been resolved already (calls
    /// project.GetDataType to find it). This should not be done when transactions are in the middle
    /// of being applied, since newly created data may not exist yet if it's still being
    /// deserialized from network packets. It should be safe to resolve it after a transaction is
    /// finished.
    /// </summary>
    public T Instance
    {
        get
        {
            if (!resolveImplicitly && !ResolvedValue)
                throw new Exception($"Tried to access unresolved InstanceResolver: {FullIdentifier}");
            return Resolve();
        }
    }

    /// <summary>
    /// Identifier (not including the type name)
    /// </summary>
    public string Identifier { get; }

    public string FullIdentifier { get { return Project.GetFullIdentifier(InstanceType, Identifier); } }

    /// <summary>
    /// May not be the same as T if there's polymorphism
    /// </summary>
    public Type InstanceType { get; }

    public bool ResolvedValue { get; private set; }

    // ================================================================================
    // Methods
    // ================================================================================

    /// <summary>
    /// Explicitly resolve the instance, if it hasn't been already.
    /// </summary>
    public T Resolve()
    {
        if (!ResolvedValue)
        {
            // Assert that we're not in the middle of processing a packet - otherwise the data
            // we're referencing might not exist yet!
            // (TODO: Ensure this check is accurate when networking is implemented)
            // Helper.Assert(!project.UndoState.IsUndoing,
            //               "Tried to resolve an instance in the middle of an undo!");

            instance = (T)project.GetDataType(InstanceType, Identifier, createIfMissing: true);
            ResolvedValue = true;
        }
        else
        {
            #if DEBUG
            // Check for potential edge case where we attempt to access something that was
            // removed from the project's tracking (which can happen on undos).
            if (!project.CheckHasDataType(InstanceType, Identifier))
                throw new Exception($"Project missing data: {FullIdentifier}");
            // Check for stale references
            if (project.GetDataType(InstanceType, Identifier, createIfMissing: false) != instance)
                throw new Exception($"InstanceResolver has stale data for: {FullIdentifier}");
            #endif
        }
        return instance;
    }

    public void Unresolve()
    {
        ResolvedValue = false;
        instance = null;
    }

    public void Reresolve()
    {
        Unresolve();
        Resolve();
    }

    public override bool Equals(object obj)
    {
        return obj is InstanceResolver<T> other
            && this.Identifier == other.Identifier
            && this.project == other.project;
    }

    public override int GetHashCode() => Identifier.GetHashCode() + project.GetHashCode() + typeof(T).GetHashCode();


    /// <summary>
    /// Implicit conversion to the object this is referencing
    /// </summary>
    public static implicit operator T(InstanceResolver<T> r)
    {
        return r.Instance;
    }

    public static bool operator==(InstanceResolver<T> r1, InstanceResolver<T> r2)
    {
        return (!(r1 is null) && r1.Equals(r2)) || (r1 is null && r2 is null);
    }

    public static bool operator!=(InstanceResolver<T> r1, InstanceResolver<T> r2)
    {
        return !(r1 == r2);
    }
}

/// <summary>
/// Implements serialization and deserialization for InstanceResolver<T>.
///
/// It's a lot of code just to turn it into a simple pair of values: "type" and "id", which together
/// are enough to uniquely identify what's being referenced.
/// </summary>
public class InstanceResolverConverter : JsonConverterFactory
{
    private Project project;

    public InstanceResolverConverter(Project p)
    {
        this.project = p;
    }

    public override bool CanConvert(Type typeToConvert)
    {
        if (!typeToConvert.IsGenericType)
            return false;

        return typeToConvert.GetGenericTypeDefinition() == typeof(InstanceResolver<>);
    }

    public override JsonConverter CreateConverter(
        Type type,
        JsonSerializerOptions options)
    {
        Type[] typeArguments = type.GetGenericArguments();
        Type genericType = typeArguments[0];

        JsonConverter converter = (JsonConverter)Activator.CreateInstance(
            typeof(InstanceResolverInner<>).MakeGenericType([genericType]),
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            args: [project, options],
            culture: null)!;

        return converter;
    }

    private class InstanceResolverInner<T> :
        JsonConverter<InstanceResolver<T>> where T : ProjectDataType
    {
        private Project project;
        private readonly Type resolverType;
        private readonly Type instanceType;

        public InstanceResolverInner(Project project, JsonSerializerOptions options)
        {
            this.project = project;
            this.resolverType = typeof(InstanceResolver<T>);
            this.instanceType = typeof(T);
        }

        public override InstanceResolver<T> Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (resolverType != typeToConvert)
                throw new JsonException($"Type mismatch: {resolverType.FullName} != {typeToConvert.FullName}");

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Expected StartObject token.");
            }

            string typeStr = null, id = null;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    if (typeStr == null || id == null)
                        throw new JsonException("Missing type or id.");
                    if (typeStr != instanceType.FullName)
                        throw new JsonException($"Type mismatch: {instanceType.FullName} != {typeStr}");
                    return new InstanceResolver<T>(project, typeStr, id);
                }
                else if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException($"Unexpected token type {reader.TokenType}.");
                }

                string propertyName = reader.GetString();

                reader.Read();

                if (reader.TokenType != JsonTokenType.String)
                    throw new JsonException();

                if (propertyName == "id")
                    id = reader.GetString();
                else if (propertyName == "type")
                    typeStr = reader.GetString();
                else
                    throw new JsonException($"Unrecognized property name {propertyName}.");
            }
            throw new JsonException("Unexpected end of data");
        }

        public override void Write(
            Utf8JsonWriter writer,
            InstanceResolver<T> resolver,
            JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WriteString("type", instanceType.FullName);
            writer.WriteString("id", resolver.Identifier);

            writer.WriteEndObject();
        }
    }
}
