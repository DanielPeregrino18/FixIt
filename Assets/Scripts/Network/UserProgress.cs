using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

[Serializable]
public class UserProgress
{
    public string userId;
    public WorldProgressEntry[] worlds = new WorldProgressEntry[0];
    public string updatedAt;

    public WorldProgress GetWorld(string worldId)
    {
        if (worlds == null) return null;

        for (int i = 0; i < worlds.Length; i++)
            if (worlds[i] != null && worlds[i].key == worldId)
                return worlds[i].value;

        return null;
    }

    /// <summary>
    /// Convierte el objeto worlds del backend (un mapa de claves dinámicas) a
    /// arreglos serializables, que JsonUtility y el Inspector de Unity soportan.
    /// </summary>
    public static UserProgress FromJson(string json)
    {
        object parsed = JsonValueParser.Parse(json);
        Dictionary<string, object> root = parsed as Dictionary<string, object>;
        if (root == null)
            throw new FormatException("La respuesta de progreso debe ser un objeto JSON.");

        object progressValue;
        if (root.TryGetValue("progress", out progressValue))
        {
            Dictionary<string, object> wrappedProgress = progressValue as Dictionary<string, object>;
            if (wrappedProgress != null) root = wrappedProgress;
        }

        UserProgress progress = new UserProgress();
        progress.userId = GetString(root, "userId");
        progress.updatedAt = GetString(root, "updatedAt");

        object worldsValue;
        Dictionary<string, object> worldMap = root.TryGetValue("worlds", out worldsValue)
            ? worldsValue as Dictionary<string, object>
            : null;

        if (worldMap == null) return progress;

        progress.worlds = new WorldProgressEntry[worldMap.Count];
        int worldIndex = 0;
        foreach (KeyValuePair<string, object> worldPair in worldMap)
        {
            Dictionary<string, object> worldObject = worldPair.Value as Dictionary<string, object>;
            WorldProgress world = new WorldProgress();
            Dictionary<string, object> levelMap = null;
            object levelsValue;

            if (worldObject != null && worldObject.TryGetValue("levels", out levelsValue))
                levelMap = levelsValue as Dictionary<string, object>;

            if (levelMap != null)
            {
                world.levels = new LevelProgressEntry[levelMap.Count];
                int levelIndex = 0;
                foreach (KeyValuePair<string, object> levelPair in levelMap)
                {
                    Dictionary<string, object> levelObject = levelPair.Value as Dictionary<string, object>;
                    LevelProgress level = new LevelProgress();
                    if (levelObject != null)
                    {
                        level.bestScore = GetInt(levelObject, "bestScore");
                        level.bestStars = GetInt(levelObject, "bestStars");
                    }

                    world.levels[levelIndex++] = new LevelProgressEntry
                    {
                        key = levelPair.Key,
                        value = level
                    };
                }
            }

            progress.worlds[worldIndex++] = new WorldProgressEntry
            {
                key = worldPair.Key,
                value = world
            };
        }

        return progress;
    }

    private static string GetString(Dictionary<string, object> source, string key)
    {
        object value;
        return source.TryGetValue(key, out value) && value != null
            ? Convert.ToString(value, CultureInfo.InvariantCulture)
            : null;
    }

    private static int GetInt(Dictionary<string, object> source, string key)
    {
        object value;
        if (!source.TryGetValue(key, out value) || value == null) return 0;

        try
        {
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (exception is FormatException || exception is InvalidCastException || exception is OverflowException)
        {
            return 0;
        }
    }
}

[Serializable]
public class WorldProgressEntry
{
    public string key;
    public WorldProgress value;
}

[Serializable]
public class WorldProgress
{
    public LevelProgressEntry[] levels = new LevelProgressEntry[0];

    public LevelProgress GetLevel(string levelId)
    {
        if (levels == null) return null;

        for (int i = 0; i < levels.Length; i++)
            if (levels[i] != null && levels[i].key == levelId)
                return levels[i].value;

        return null;
    }
}

[Serializable]
public class LevelProgressEntry
{
    public string key;
    public LevelProgress value;
}

[Serializable]
public class LevelProgress
{
    public int bestScore;
    public int bestStars;
}

internal sealed class JsonValueParser
{
    private readonly string _json;
    private int _position;

    private JsonValueParser(string json)
    {
        _json = json ?? throw new ArgumentNullException(nameof(json));
    }

    public static object Parse(string json)
    {
        JsonValueParser parser = new JsonValueParser(json);
        object value = parser.ParseValue();
        parser.SkipWhitespace();
        if (parser._position != parser._json.Length)
            throw new FormatException("Hay contenido adicional después del JSON de progreso.");
        return value;
    }

    private object ParseValue()
    {
        SkipWhitespace();
        if (_position >= _json.Length) throw new FormatException("JSON de progreso incompleto.");

        char current = _json[_position];
        if (current == '{') return ParseObject();
        if (current == '[') return ParseArray();
        if (current == '"') return ParseString();
        if (current == 't') return ParseLiteral("true", true);
        if (current == 'f') return ParseLiteral("false", false);
        if (current == 'n') return ParseLiteral("null", null);
        return ParseNumber();
    }

    private Dictionary<string, object> ParseObject()
    {
        Dictionary<string, object> result = new Dictionary<string, object>();
        _position++;
        SkipWhitespace();
        if (Consume('}')) return result;

        while (true)
        {
            SkipWhitespace();
            if (_position >= _json.Length || _json[_position] != '"')
                throw new FormatException("Se esperaba una clave de objeto en el JSON de progreso.");

            string key = ParseString();
            SkipWhitespace();
            Require(':');
            result[key] = ParseValue();
            SkipWhitespace();
            if (Consume('}')) return result;
            Require(',');
        }
    }

    private List<object> ParseArray()
    {
        List<object> result = new List<object>();
        _position++;
        SkipWhitespace();
        if (Consume(']')) return result;

        while (true)
        {
            result.Add(ParseValue());
            SkipWhitespace();
            if (Consume(']')) return result;
            Require(',');
        }
    }

    private string ParseString()
    {
        Require('"');
        StringBuilder result = new StringBuilder();

        while (_position < _json.Length)
        {
            char current = _json[_position++];
            if (current == '"') return result.ToString();
            if (current != '\\')
            {
                result.Append(current);
                continue;
            }

            if (_position >= _json.Length) throw new FormatException("Cadena JSON incompleta.");
            char escaped = _json[_position++];
            switch (escaped)
            {
                case '"': result.Append('"'); break;
                case '\\': result.Append('\\'); break;
                case '/': result.Append('/'); break;
                case 'b': result.Append('\b'); break;
                case 'f': result.Append('\f'); break;
                case 'n': result.Append('\n'); break;
                case 'r': result.Append('\r'); break;
                case 't': result.Append('\t'); break;
                case 'u':
                    if (_position + 4 > _json.Length) throw new FormatException("Escape Unicode incompleto en el JSON.");
                    string hex = _json.Substring(_position, 4);
                    result.Append((char)int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                    _position += 4;
                    break;
                default:
                    throw new FormatException("Escape no válido en el JSON de progreso.");
            }
        }

        throw new FormatException("Cadena JSON sin cerrar.");
    }

    private object ParseNumber()
    {
        int start = _position;
        while (_position < _json.Length && "-+0123456789.eE".IndexOf(_json[_position]) >= 0)
            _position++;

        if (start == _position) throw new FormatException("Valor JSON no válido.");
        string number = _json.Substring(start, _position - start);
        long integerValue;
        if (long.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out integerValue))
            return integerValue;

        double decimalValue;
        if (double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out decimalValue))
            return decimalValue;

        throw new FormatException("Número no válido en el JSON de progreso.");
    }

    private object ParseLiteral(string literal, object value)
    {
        if (_position + literal.Length > _json.Length ||
            string.CompareOrdinal(_json, _position, literal, 0, literal.Length) != 0)
            throw new FormatException("Literal JSON no válido.");

        _position += literal.Length;
        return value;
    }

    private void SkipWhitespace()
    {
        while (_position < _json.Length && char.IsWhiteSpace(_json[_position])) _position++;
    }

    private bool Consume(char expected)
    {
        if (_position >= _json.Length || _json[_position] != expected) return false;
        _position++;
        return true;
    }

    private void Require(char expected)
    {
        if (!Consume(expected)) throw new FormatException("JSON de progreso mal formado.");
    }
}