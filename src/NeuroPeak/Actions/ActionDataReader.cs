using NeuroSdk.Actions;
using NeuroSdk.Json;

namespace NeuroPeak.Actions
{
    public static class ActionDataReader
    {
        public static string? ReadString(ActionJData actionData, string key)
        {
            try
            {
                return actionData.GetValue<string>(key);
            }
            catch
            {
                return null;
            }
        }

        public static bool TryReadFloat(ActionJData actionData, string key, out float value)
        {
            value = 0f;
            try
            {
                if (actionData.Data?[key] == null) return false;
                value = actionData.GetValue(key, 0f);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryReadBool(ActionJData actionData, string key, out bool value)
        {
            value = false;
            try
            {
                if (actionData.Data?[key] == null) return false;
                value = actionData.GetValue(key, false);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
