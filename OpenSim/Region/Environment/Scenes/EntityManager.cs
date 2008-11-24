﻿using System;
using System.Collections.Generic;
using OpenMetaverse;

namespace OpenSim.Region.Environment.Scenes
{
    public class EntityManager
    {
        private readonly Dictionary<UUID,EntityBase> m_eb_uuid = new Dictionary<UUID, EntityBase>();
        private readonly Dictionary<uint, EntityBase> m_eb_localID = new Dictionary<uint, EntityBase>();
        private readonly Object m_lock = new Object();

        [Obsolete("Use Add() instead.")]
        public void Add(UUID id, EntityBase eb)
        {
            Add(eb);
        }

        public void Add(EntityBase entity)
        {
            lock(m_lock)
            {
                m_eb_uuid.Add(entity.UUID, entity);
                m_eb_localID.Add(entity.LocalId, entity);
            }
        }

        public void InsertOrReplace(EntityBase entity)
        {
            lock(m_lock)
            {
                m_eb_uuid[entity.UUID] = entity;
                m_eb_localID[entity.LocalId] = entity;
            }
        }

        public void Clear()
        {
            lock (m_lock)
            {
                m_eb_uuid.Clear();
                m_eb_localID.Clear();
            }
        }

        public int Count
        {
            get
            {
                lock (m_lock)
                {
                    return m_eb_uuid.Count;
                }
            }
        }

        public bool ContainsKey(UUID id)
        {
            lock(m_lock)
            {
                return m_eb_uuid.ContainsKey(id);
            }
        }

        public bool ContainsKey(uint localID)
        {
            lock (m_lock)
            {
                return m_eb_localID.ContainsKey(localID);
            }
        }

        public void Remove(uint localID)
        {
            lock(m_lock)
            {
                m_eb_uuid.Remove(m_eb_localID[localID].UUID);
                m_eb_localID.Remove(localID);
            }
        }

        public void Remove(UUID id)
        {
            lock(m_lock)
            {
                m_eb_localID.Remove(m_eb_uuid[id].LocalId);
                m_eb_uuid.Remove(id);
            }
        }

        public List<EntityBase> GetAllByType<T>()
        {
            List<EntityBase> tmp = new List<EntityBase>();

            lock(m_lock)
            {
                foreach (KeyValuePair<UUID, EntityBase> pair in m_eb_uuid)
                {
                    if(pair.Value is T)
                    {
                        tmp.Add(pair.Value);
                    }
                }
            }

            return tmp;
        }

        [Obsolete("Please used indexed access to this instead. Indexes can be accessed via EntitiesManager[index]. LocalID and UUID are supported.")]
        public List<EntityBase> GetEntities()
        {
            lock (m_lock)
            {
                return new List<EntityBase>(m_eb_uuid.Values);
            }
        }

        public EntityBase this[UUID id]
        {
            get
            {
                lock (m_lock)
                {
                    return m_eb_uuid[id];
                }
            }
            set
            {
                InsertOrReplace(value);
            }
        }

        public EntityBase this[uint localID]
        {
            get
            {
                lock (m_lock)
                {
                    return m_eb_localID[localID];
                }
            }
            set
            {
                InsertOrReplace(value);
            }
        }
    }
}