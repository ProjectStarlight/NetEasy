﻿using NetSerializer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace NetEasy
{
    /// <summary>
    /// Provides methods to send, write, and receive <see cref="ModPacket"/> objects in a modular fashion. <para/>
    /// All types deriving from this class must have the <see cref="SerializableAttribute"/>.
    /// Use the <see cref="NonSerializedAttribute"/> on fields to not serialize them.
    /// </summary>
    /// <exception cref="ModuleLoadException">Thrown when a module or its observed members do not have the <see cref="SerializableAttribute"/>.</exception>
    [Serializable]
    public abstract class Module
    {
        private static Dictionary<Type, string> typeModMap;
        private static Serializer serializer;

        /// <summary>The mod responsible for this <see cref="Module"/>.</summary>
#pragma warning disable IDE1006 // Naming Styles
        protected Mod mod => ModLoader.GetMod(typeModMap[GetType()]);
#pragma warning restore IDE1006 // Naming Styles

        /// <summary>The sender of the net message.</summary>
        protected int Sender { get; private set; } = Main.myPlayer;

        /// <summary>Initializes a new instance of the <see cref="Module"/> class.</summary>
        protected Module() { }

        /// <summary>
        /// Sends this module as a <see cref="ModPacket"/>.
        /// Does not send fields marked with <see cref="NonSerializedAttribute"/>.
        /// </summary>
        /// <param name="toClient">If not null, the packet will <b>only</b> be sent to the specified node.</param>
        /// <param name="ignoreClient">If not null, the packet will <b>not</b> be sent to the specified node.</param>
        /// <param name="runLocally">If true, the <see cref="Receive()"/> method will also be called for the sender.</param>
        public void Send(int toClient = -1, int ignoreClient = -1, bool runLocally = true)
        {
            if (PreSend(toClient, ignoreClient))
            {
                if (Main.netMode != NetmodeID.SinglePlayer)
                {
                    ModPacket mp = mod.GetPacket();
                    serializer.Serialize(mp.BaseStream, this);
                    mp.Send(toClient, ignoreClient);
                }
                if (runLocally)
                {
                    Receive();
                }
            }
        }

        /// <summary>Called after receiving the module. Use this to perform behavior after <see cref="Send"/> is called.</summary>
        protected internal abstract void Receive();

        /// <summary>Called before sending the module. Return <see langword="false"/> to cancel the send and prevent <see cref="Receive()"/> from being called. Defaults to <see langword="true"/>.</summary>
        protected virtual bool PreSend(int toClient = -1, int ignoreClient = -1) => true;

        internal static void Receive(Stream stream, int whoAmI)
        {
            Module module = (Module)serializer.Deserialize(stream);
            module.Sender = whoAmI;
            module.Receive();
        }

        internal static void Load(Mod mod)
        {
            if (typeModMap == null)
            {
                typeModMap = new Dictionary<Type, string>();
            }

            if (serializer == null)
            {
                serializer = new Serializer(Types());
            } else
            {
                serializer.AddTypes(Types());
            }
            

            IEnumerable<Type> Types()
            {
                foreach (var type in mod.Code.GetExportedTypes().Where(t => t.IsSubclassOf(typeof(Module)) && !t.IsAbstract))
                {
                    if (!type.IsSerializable)
                    {
                        throw new ModuleLoadException($"All Modules must have the SerializableAttribute. Add the SerializableAttribute to the type.", type);
                    }
                    foreach (var field in type.GetFields())
                    {
                        if (!field.FieldType.IsSerializable && !field.IsNotSerialized)
                        {
                            throw new ModuleLoadException($"The member {field.Name} is not serializable. Add the NonSerializedAttribute to it.", type);
                        }
                    }
                    typeModMap.Add(type, mod.Name);
                    yield return type;
                }
            }
        }

        internal static void Unload()
        {
            typeModMap = null;
            serializer = null;
        }
    }
}
