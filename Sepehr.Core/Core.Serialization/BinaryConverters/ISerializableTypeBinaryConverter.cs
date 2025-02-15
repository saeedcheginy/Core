﻿using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;

namespace Core.Serialization.BinaryConverters
{
    public class ISerializableTypeBinaryConverter : BinaryConverter<ISerializable>
    {
        private static StreamingContext _context = new StreamingContext();

        public ObjectMetaData EntityMetaData { get; private set; }

        public Type ObjectType { get; private set; }
        public ConstructorInfo TargetCreateInstanceMethod { get; private set; }
        public object UserDefinedObject { get; private set; }

        protected BinaryConverterBase[] BinaryConverters
        {
            get; set;
        }

        protected Type[] BinaryConverterTypesForISerializable { get; private set; }

        protected string[] ItemNamesForISerializable { get; private set; }
        public override BinaryConverterBase Copy(Type type)
        {
            var binaryConverter = new ISerializableTypeBinaryConverter();
            binaryConverter.ObjectType = type;
            binaryConverter.Init(type);
            var createInstanceReturnType = typeof(object);
            var createInstanceParamsTypes = new[] { typeof(SerializationInfo), typeof(StreamingContext) };
            binaryConverter.TargetCreateInstanceMethod = binaryConverter.CurrentType.GetConstructor(createInstanceParamsTypes);
            if (binaryConverter.TargetCreateInstanceMethod == null)
            {
                binaryConverter.TargetCreateInstanceMethod = binaryConverter.CurrentType.GetConstructor(
BindingFlags.NonPublic | BindingFlags.CreateInstance | BindingFlags.Instance,
null,
createInstanceParamsTypes,
null
);
            }
            binaryConverter.EntityMetaData = ObjectMetaData.GetEntityMetaData(type);
            binaryConverter.UserDefinedObject = Activator.CreateInstance(type);
            return binaryConverter;
        }

        private bool? IsCustomInited { get; set; }
        private void CustomInit()
        {
            if (IsCustomInited == null)
            {
                lock (this)
                {
                    if (IsCustomInited == null)
                    {
                        SerializationInfo serializationInfo = new SerializationInfo(CurrentType, new FormatterConverter());
                        ((ISerializable)UserDefinedObject).GetObjectData(serializationInfo, _context);
                        ItemNamesForISerializable = new string[serializationInfo.MemberCount];
                        BinaryConverters = new BinaryConverterBase[serializationInfo.MemberCount];
                        BinaryConverterTypesForISerializable = new Type[serializationInfo.MemberCount];
                        var i = 0;
                        foreach (SerializationEntry serializationEntry in serializationInfo)
                        {
                            BinaryConverters[i] = BinaryConverterBase.GetBinaryConverter(serializationEntry.ObjectType);
                            BinaryConverterTypesForISerializable[i] = serializationEntry.ObjectType;
                            ItemNamesForISerializable[i] = serializationEntry.Name;
                            i++;
                        }

                        IsCustomInited = true;
                    }
                }
            }
        }

        public override object CreateInstance(BinaryReader reader, Type objectType, DeserializationContext context)
        {
            var obj = EntityMetaData.ReflectionEmitPropertyAccessor.EmittedObjectInstanceCreator();
            context.CurrentReferenceTypeObject = obj;
            return obj;
        }
        protected override ISerializable DeserializeBase(BinaryReader reader, Type objectType, DeserializationContext context)
        {
            CustomInit();
            ISerializable result = (ISerializable)context.CurrentReferenceTypeObject;
            if (!FullyTrusted)
            {
                string message = $@"Type '{CurrentType}' implements ISerializable but cannot be serialized using the ISerializable interface because the current application is not fully trusted and ISerializable can expose secure data." + Environment.NewLine +
                                 @"To fix this error either change the environment to be fully trusted, change the application to not deserialize the type, add JsonObjectAttribute to the type or change the JsonSerializer setting ContractResolver to use a new DefaultContractResolver with IgnoreSerializableInterface set to true." + Environment.NewLine;
                throw new SerializationException(message);
            }

            var memberCount = BinaryConverters.Length;
            SerializationInfo serializationInfo = new SerializationInfo(CurrentType, new FormatterConverter());
            for (var i = 0; i < memberCount; i++)
            {
                object value;
                value = BinaryConverters[i].Deserialize(reader, BinaryConverterTypesForISerializable[i], context);
                serializationInfo.AddValue(ItemNamesForISerializable[i], value, BinaryConverterTypesForISerializable[i]);
            }

            TargetCreateInstanceMethod.Invoke(result, new object[] { serializationInfo, _context });
            //result = (ISerializable)EntityMetaData.ReflectionEmitPropertyAccessor.CreateInstanceForISerialzableObject(serializationInfo, _context);
            return result;
        }

        protected override void SerializeBase(ISerializable objectItem, BinaryWriter writer, SerializationContext context)
        {
            CustomInit();
            if (!FullyTrusted)
            {
                string message = $@"Type '{CurrentType}' implements ISerializable but cannot be serialized using the ISerializable interface because the current application is not fully trusted and ISerializable can expose secure data." + Environment.NewLine +
                                 @"To fix this error either change the environment to be fully trusted, change the application to not deserialize the type, add JsonObjectAttribute to the type or change the JsonSerializer setting ContractResolver to use a new DefaultContractResolver with IgnoreSerializableInterface set to true." + Environment.NewLine;
                throw new SerializationException(message);
            }

            SerializationInfo serializationInfo = new SerializationInfo(CurrentType, new FormatterConverter());
            ((ISerializable)objectItem).GetObjectData(serializationInfo, _context);
            var i = 0;
            foreach (SerializationEntry serializationEntry in serializationInfo)
            {
                BinaryConverters[i].Serialize(serializationEntry.Value, writer, context);
                i++;
            }
        }
    }
}