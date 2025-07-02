#nullable enable

using System.IO;
using System.Text.Json;

namespace LynnaLib;

// Functions relating to reading/writing annotation data
public partial class Project
{
    int AnnotationIDCounter
    {
        get { return stateHolder.State.annotationIDCounter; }
        set { stateHolder.State.annotationIDCounter = value; }
    }

    IEnumerable<Annotation> Annotations {
        get { return stateHolder.State.annotations.Select((a) => a.Instance); }
    }
    string AnnotationsFile { get { return $"{BaseDirectory}/LynnaLab/annotations.json"; } }

    /// <summary>
    /// Add an annotation to the specified room.
    /// </summary>
    public void AddAnnotation(int roomIndex)
    {
        BeginTransaction("Add Annotation");
        CaptureSelfInitialState();
        Annotation ann = new(this, AnnotationIDCounter++, roomIndex);
        stateHolder.State.annotations.Add(new(ann));
        AnnotationsAddedOrRemovedEvent?.Invoke();
        this.Modified = true;
        EndTransaction();
    }

    /// <summary>
    /// Delete the given annotation from the project.
    /// </summary>
    public void DeleteAnnotation(Annotation ann)
    {
        BeginTransaction("Delete Annotation");
        CaptureSelfInitialState();
        if (!stateHolder.State.annotations.Remove(new(ann)))
        {
            throw new Exception("DeleteAnnotation: Tried to delete a non-existing annotation?");
        }
        AnnotationsAddedOrRemovedEvent?.Invoke();
        this.Modified = true;
        EndTransaction();
    }

    /// <summary>
    /// Get all the annotations in a room.
    /// </summary>
    public IEnumerable<Annotation> GetRoomAnnotations(int roomIndex)
    {
        foreach (var ann in Annotations)
        {
            if (ann.RoomIndex == roomIndex)
                yield return ann;
        }
    }

    /// <summary>
    /// Attempt to load annotations file. This should not throw any exceptions, instead writing to
    /// the log if errors occur.
    /// </summary>
    void LoadAnnotations()
    {
        if (!IsInConstructor)
            throw new Exception();

        stateHolder.State.annotations.Clear();

        if (!File.Exists(AnnotationsFile))
        {
            return;
        }

        try
        {
            using (Stream stream = new FileStream(AnnotationsFile, FileMode.Open))
            {
                AnnotationFileDTO? dto = JsonSerializer.Deserialize<AnnotationFileDTO>(stream, this.SerializerOptions);
                if (dto == null)
                {
                    throw new JsonException("LoadAnnotations: Deserialized to null");
                }
                foreach (var annDTO in dto.AnnotationList)
                {
                    stateHolder.State.annotations.Add(new(new Annotation(this, AnnotationIDCounter++, annDTO)));
                }
            }
        }
        catch (Exception e)
        {
            // The exception should be either:
            // - A JsonException, or
            // - A file access exception (ie. IOException, UnauthorizedAccessException)
            log.Error(e);
        }
    }

    /// <summary>
    /// Can throw FileNotFoundException, UnauthorizedAccessException, etc.
    /// </summary>
    void SaveAnnotations()
    {
        using (Stream stream = new FileStream(AnnotationsFile, FileMode.Create))
        {
            List<AnnotationDTO> dtoList = Annotations.Select((a) => a.AsDTO()).ToList();
            AnnotationFileDTO dto = new()
            {
                AnnotationList = dtoList
            };
            JsonSerializer.Serialize<AnnotationFileDTO>(stream, dto, this.SerializerOptions);
        }
    }
}

/// <summary>
/// An annotation is a note placed at a specific position in a particular room, just for developers.
/// </summary>
public class Annotation : TrackedIndexedProjectData
{
    // ================================================================================
    // Constructors
    // ================================================================================

    /// <summary>
    /// Constructor for newly created annotations
    /// </summary>
    internal Annotation(Project p, int id, int room)
        : base(p, id)
    {
        this.state = new()
        {
            roomIndex = room,
            x = 0x58,
            y = 0x58,
            text = "",
            letter = 'A',
        };
    }

    /// <summary>
    /// Constructor for loading from JSON file
    /// </summary>
    internal Annotation(Project p, int id, AnnotationDTO dto)
        : base(p, id)
    {
        this.state = new()
        {
            roomIndex = dto.RoomIndex,
            x = dto.X,
            y = dto.Y,
            text = dto.Text,
            letter = dto.Letter,
        };
    }

    /// <summary>
    /// State-based constructor, for network transfer (located via reflection)
    /// </summary>
    private Annotation(Project p, string id, TransactionState s)
        : base(p, int.Parse(id))
    {
        this.state = (State)s;

        if (this.state.text.Length > MAX_TEXT_LENGTH)
            this.state.text = this.state.text.Substring(0, MAX_TEXT_LENGTH);

        if (!(state.letter >= 'A' && state.letter <= 'Z'))
            state.letter = 'A';
    }

    // ================================================================================
    // Variables
    // ================================================================================

    public const int MAX_TEXT_LENGTH = 1024;

    class State : TransactionState
    {
        public required int roomIndex;
        public required byte x;
        public required byte y;
        public required string text;
        public required char letter;
    }

    State state;

    // ================================================================================
    // Properties
    // ================================================================================

    public string TransactionIdentifier { get { return $"annotation-{Index}"; } }

    public int RoomIndex { get { return state.roomIndex; } }

    public byte X
    {
        get
        {
            return state.x;
        }
        set
        {
            Project.BeginTransaction("Set annotation X");
            Project.TransactionManager.CaptureInitialState<State>(this);
            state.x = value;
            Project.EndTransaction();
        }
    }

    public byte Y
    {
        get
        {
            return state.y;
        }
        set
        {
            Project.BeginTransaction("Set annotation Y");
            Project.TransactionManager.CaptureInitialState<State>(this);
            state.y = value;
            Project.EndTransaction();
        }
    }

    public string Text
    {
        get
        {
            return state.text;
        }
        set
        {
            Project.BeginTransaction($"Set annotation text#{TransactionIdentifier}", merge: true);
            Project.TransactionManager.CaptureInitialState<State>(this);
            state.text = value;
            Project.EndTransaction();
        }
    }

    public char Letter
    {
        get { return state.letter; }
        set
        {
            Project.BeginTransaction($"Set annotation letter#{TransactionIdentifier}", merge: true);
            Project.TransactionManager.CaptureInitialState<State>(this);
            state.letter = value;
            Project.EndTransaction();
        }
    }

    // ================================================================================
    // Public methods
    // ================================================================================

    public void Delete()
    {
        Project.DeleteAnnotation(this);
    }

    internal AnnotationDTO AsDTO()
    {
        return new AnnotationDTO()
        {
            RoomIndex = RoomIndex,
            X = X,
            Y = Y,
            Text = Text,
            Letter = Letter,
        };
    }

    // ================================================================================
    // TrackedProjectData interface functions
    // ================================================================================

    public override TransactionState GetState()
    {
        return state;
    }

    public override void SetState(TransactionState s)
    {
        state = (State)s;
    }

    public override void InvokeUndoEvents(TransactionState prevState)
    {
        Project.MarkModified();
    }
}

class AnnotationFileDTO
{
    public required List<AnnotationDTO> AnnotationList { get; init; }
}

class AnnotationDTO
{
    public required int RoomIndex { get; init; }
    public required byte X { get; init; }
    public required byte Y { get; init; }
    public required string Text { get; init; }
    public required char Letter { get; init; }
}
