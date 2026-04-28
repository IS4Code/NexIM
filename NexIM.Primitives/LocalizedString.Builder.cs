using System.Runtime.InteropServices;
using NexIM.Tools;

namespace NexIM.Primitives;

partial struct LocalizedString
{
    public Builder ToBuilder()
    {
        return new(data.ToBuilder());
    }

    [StructLayout(LayoutKind.Auto)]
    public struct Builder
    {
        NonEmptyDictionary<LanguageCode, ValueString>.Builder builder;

        internal Builder(NonEmptyDictionary<LanguageCode, ValueString>.Builder builder)
        {
            this.builder = builder;
        }

        public void Add(LanguageTaggedString str)
        {
            builder.Add(str.Language, new(str.Value));
        }

        public void Add(LanguageTaggedString? str)
        {
            if(str is { } value)
            {
                Add(value);
            }
        }

        public void Add(LocalizedString str)
        {
            builder.Add(str.data);
        }

        public void Add(LocalizedString? str)
        {
            if(str is { } value)
            {
                Add(value);
            }
        }

        public LocalizedString? TryToString()
        {
            if(builder.TryToDictionary() is not { } dict)
            {
                return null;
            }
            return new(dict);
        }
    }
}
