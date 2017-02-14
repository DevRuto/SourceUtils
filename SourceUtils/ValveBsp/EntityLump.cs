﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using SourceUtils.ValveBsp.Entities;

namespace SourceUtils
{
    partial class ValveBspFile
    {
        public class EntityLump : ILump, IEnumerable<Entity>
        {
            private static readonly Dictionary<string, Func<Entity>> _sEntityCtors = new Dictionary<string, Func<Entity>>();

            static EntityLump()
            {
                foreach ( var type in Assembly.GetExecutingAssembly().GetTypes() )
                {
                    if ( !typeof(Entity).IsAssignableFrom( type ) ) continue;
                    var attrib = type.GetCustomAttribute<EntityClassAttribute>();
                    if ( attrib == null ) continue;

                    var ctor = type.GetConstructor( Type.EmptyTypes );
                    if ( ctor == null ) continue;

                    var action = Expression.Lambda<Func<Entity>>( Expression.New( ctor ) ).Compile();

                    _sEntityCtors.Add( attrib.Name, action );
                }
            }

            public LumpType LumpType { get; }

            private readonly ValveBspFile _bspFile;

            private List<Entity> _entities;

            public int Count
            {
                get
                {
                    EnsureLoaded();
                    return _entities.Count;
                }
            }

            public Entity this[ int index ]
            {
                get
                {
                    EnsureLoaded();
                    return _entities[index];
                }
            }

            public EntityLump( ValveBspFile bspFile, LumpType type )
            {
                _bspFile = bspFile;
                LumpType = type;
            }

            private void EnsureLoaded()
            {
                if ( _entities != null ) return;

                _entities = new List<Entity>();

                var propBuffer = new List<KeyValuePair<string, string>>();

                using ( var reader = new StreamReader( _bspFile.GetLumpStream( LumpType ) ) )
                {
                    string line;
                    while ( (line = reader.ReadLine()) != null )
                    {
                        if ( line != "{" ) continue;

                        string className;

                        propBuffer.Clear();
                        ReadPropertyGroup( reader, propBuffer, out className );
                        
                        Func<Entity> ctor;
                        var ent = _sEntityCtors.TryGetValue( className, out ctor ) ? ctor() : new Entity();
                        ent.Initialize( propBuffer );

                        _entities.Add( ent );
                    }
                }
            }

            private static readonly Regex _sPropertyRegex = new Regex( @"^\s*""(?<name>([^""]|\\.)+)""\s*""(?<value>([^""]|\\.)*)""\s*$", RegexOptions.Compiled );

            private static void ReadPropertyGroup( StreamReader reader, List<KeyValuePair<string, string>> propertyBuffer, out string className )
            {
                className = null;

                string line;
                while ( (line = reader.ReadLine()) != null )
                {
                    if ( line == "}" ) break;

                    var match = _sPropertyRegex.Match( line );
                    if ( !match.Success ) throw new Exception( $"Unexpected value while reading entity lump ({line})." );

                    var name = match.Groups["name"].Value;
                    var value = match.Groups["value"].Value;

                    if ( name == "classname" ) className = value;

                    propertyBuffer.Add( new KeyValuePair<string, string>( name, value ) );
                }
            }

            public T GetFirst<T>()
                where T : Entity
            {
                return this.OfType<T>().FirstOrDefault();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public IEnumerator<Entity> GetEnumerator()
            {
                EnsureLoaded();
                return _entities.GetEnumerator();
            }
        }
    }

    namespace ValveBsp.Entities
    {
        [AttributeUsage(AttributeTargets.Class)]
        public class EntityClassAttribute : Attribute
        {
            public string Name { get; set; }

            public EntityClassAttribute( string name )
            {
                Name = name;
            }
        }

        [AttributeUsage(AttributeTargets.Property)]
        public class EntityFieldAttribute : Attribute
        {
            public string Name { get; set; }

            public EntityFieldAttribute( string name )
            {
                Name = name;
            }
        }

        public struct EntityProperty
        {
            public static implicit operator string( EntityProperty prop )
            {
                return prop._entity.GetRawPropertyValue( prop._name );
            }

            public static implicit operator int( EntityProperty prop )
            {
                return Entity.Converters.ToInt32( prop._entity.GetRawPropertyValue( prop._name ) );
            }

            public static implicit operator float( EntityProperty prop )
            {
                return Entity.Converters.ToSingle( prop._entity.GetRawPropertyValue( prop._name ) );
            }

            public static implicit operator Vector3( EntityProperty prop )
            {
                return Entity.Converters.ToVector3( prop._entity.GetRawPropertyValue( prop._name ) );
            }

            private readonly Entity _entity;
            private readonly string _name;

            internal EntityProperty( Entity entity, string name )
            {
                _entity = entity;
                _name = name;
            }
        }

        public class Entity
        {
            private static readonly Dictionary<Type, Dictionary<string, Action<Entity, string>>> _sPropertyActions;

            static Entity()
            {
                _sPropertyActions = new Dictionary<Type, Dictionary<string, Action<Entity, string>>>();

                foreach ( var type in Assembly.GetExecutingAssembly().GetTypes() )
                {
                    if ( !typeof(Entity).IsAssignableFrom( type ) ) continue;
                    BuildPropertyActions( type );
                }
            }

            private static Dictionary<string, Action<Entity, string>> BuildPropertyActions( Type type )
            {
                Dictionary<string, Action<Entity, string>> actions;
                if ( _sPropertyActions.TryGetValue( type, out actions ) ) return actions;

                _sPropertyActions.Add( type, actions = new Dictionary<string, Action<Entity, string>>() );
                if ( type.BaseType != null && typeof(Entity).IsAssignableFrom( type.BaseType ) )
                {
                    foreach ( var pair in BuildPropertyActions( type.BaseType ) )
                    {
                        actions.Add( pair.Key, pair.Value );
                    }
                }

                foreach ( var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public) )
                {
                    if ( property.DeclaringType != type ) continue;

                    var attrib = property.GetCustomAttribute<EntityFieldAttribute>();
                    if ( attrib == null ) continue;

                    actions.Add( attrib.Name, BuildPropertyAction( type, property ) );
                }

                return actions;
            }

            // ReSharper disable UnusedMember.Local
            // ReSharper disable MemberCanBePrivate.Local
            internal static class Converters
            {
                public static string ToString( string param )
                {
                    return param;
                }
                
                public static int ToInt32( string param )
                {
                    return int.Parse( param );
                }

                public static bool ToBoolean( string param )
                {
                    return ToInt32( param ) != 0;
                }

                public static float ToSingle( string param )
                {
                    return float.Parse( param );
                }

                public static Vector3 ToVector3( string param )
                {
                    var split0 = param.IndexOf( ' ' );
                    var split1 = param.IndexOf( ' ', split0 + 1 );

                    if ( split0 == -1 ) return new Vector3( ToSingle( param ), 0f, 0f );
                    if ( split1 == -1 ) return new Vector3( ToSingle( param.Substring( 0, split0 ) ), ToSingle( param.Substring( split0 + 1 ) ), 0f );
                    return new Vector3( ToSingle( param.Substring( 0, split0 ) ), ToSingle( param.Substring( split0 + 1, split1 - split0 ) ), ToSingle( param.Substring( split1 + 1 ) ) );
                }
            }
            // ReSharper restore MemberCanBePrivate.Local
            // ReSharper restore UnusedMember.Local

            private static Expression GetConversionExpression( Expression param, Type destType )
            {
                var method = typeof(Converters).GetMethods( BindingFlags.Public | BindingFlags.Static )
                    .FirstOrDefault( x =>
                        x.ReturnType == destType && x.GetParameters().Length == 1 &&
                        x.GetParameters()[0].ParameterType == typeof(string) );

                if (method == null) throw new NotImplementedException($"Unable to convert an entity field to type '{destType}'.");

                return Expression.Call( method, param );
            }

            private static Action<Entity, string> BuildPropertyAction( Type entityType, PropertyInfo property )
            {
                var entityParam = Expression.Parameter( typeof(Entity), "entity" );
                var castedEntity = Expression.Convert( entityParam, entityType );
                var valueParam = Expression.Parameter( typeof(string), "value" );
                var converted = GetConversionExpression( valueParam, property.PropertyType );
                var call = Expression.Call( castedEntity, property.SetMethod, converted );

                return Expression.Lambda<Action<Entity, string>>( call, entityParam, valueParam ).Compile();
            }

            private readonly Dictionary<string, string> _properties = new Dictionary<string, string>();

            [EntityField("classname")]
            public string ClassName { get; private set; }

            [EntityField("origin")]
            public Vector3 Origin { get; private set; }

            [EntityField("angles")]
            public Vector3 Angles { get; private set; }

            public IEnumerable<string> PropertyNames => _properties.Keys;

            public EntityProperty this[ string name ]
            {
                get { return new EntityProperty( this, name ); }
            }

            public string GetRawPropertyValue( string name )
            {
                string value;
                return _properties.TryGetValue( name, out value ) ? value : null;
            }

            internal void Initialize( List<KeyValuePair<string, string>> props )
            {
                foreach ( var pair in props )
                {
                    if ( _properties.ContainsKey( pair.Key ) ) _properties[pair.Key] = pair.Value;
                    else _properties.Add( pair.Key, pair.Value );
                }

                Dictionary<string, Action<Entity, string>> actions;
                if ( !_sPropertyActions.TryGetValue( GetType(), out actions ) ) return;

                foreach ( var pair in props )
                {
                    Action<Entity, string> action;
                    if ( actions.TryGetValue( pair.Key, out action ) ) action( this, pair.Value );
                }
            }
        }

        [EntityClass("worldspawn")]
        public class WorldSpawn : Entity
        {
            [EntityField("skyname")]
            public string SkyName { get; private set; }
        }
        
        [EntityClass("func_brush")]
        public class FuncBrush : Entity
        {
            [EntityField("model")]
            public string Model { get; private set; }
        }

        [EntityClass("info_player_start")]
        public class InfoPlayerStart : Entity { }

        [EntityClass("info_player_terrorist")]
        public class InfoPlayerTerrorist : InfoPlayerStart { }

        [EntityClass("info_player_counterterrorist")]
        public class InfoPlayerCounterTerrorist : InfoPlayerStart { }
    }
}