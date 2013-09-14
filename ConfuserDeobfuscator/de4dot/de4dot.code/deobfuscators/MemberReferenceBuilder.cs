﻿/*
    Copyright (C) 2011-2013 de4dot@gmail.com

    This file is part of de4dot.

    de4dot is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    de4dot is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with de4dot.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using dnlib.DotNet;
using de4dot.blocks;

namespace de4dot.code.deobfuscators {
	class MemberRefBuilder {
		ModuleDefMD module;
		Dictionary<TypeSig, TypeSig> createdTypes = new Dictionary<TypeSig, TypeSig>(TypeEqualityComparer.Instance);

		public MemberRefBuilder(ModuleDefMD module) {
			this.module = module;
		}

		public AssemblyRef CorLib {
			get { return module.CorLibTypes.AssemblyRef; }
		}

		public CorLibTypeSig Object {
			get { return module.CorLibTypes.Object; }
		}

		public CorLibTypeSig Void {
			get { return module.CorLibTypes.Void; }
		}

		public CorLibTypeSig Boolean {
			get { return module.CorLibTypes.Boolean; }
		}

		public CorLibTypeSig Char {
			get { return module.CorLibTypes.Char; }
		}

		public CorLibTypeSig SByte {
			get { return module.CorLibTypes.SByte; }
		}

		public CorLibTypeSig Byte {
			get { return module.CorLibTypes.Byte; }
		}

		public CorLibTypeSig Int16 {
			get { return module.CorLibTypes.Int16; }
		}

		public CorLibTypeSig UInt16 {
			get { return module.CorLibTypes.UInt16; }
		}

		public CorLibTypeSig Int32 {
			get { return module.CorLibTypes.Int32; }
		}

		public CorLibTypeSig UInt32 {
			get { return module.CorLibTypes.UInt32; }
		}

		public CorLibTypeSig Int64 {
			get { return module.CorLibTypes.Int64; }
		}

		public CorLibTypeSig UInt64 {
			get { return module.CorLibTypes.UInt64; }
		}

		public CorLibTypeSig Single {
			get { return module.CorLibTypes.Single; }
		}

		public CorLibTypeSig Double {
			get { return module.CorLibTypes.Double; }
		}

		public CorLibTypeSig IntPtr {
			get { return module.CorLibTypes.IntPtr; }
		}

		public CorLibTypeSig UIntPtr {
			get { return module.CorLibTypes.UIntPtr; }
		}

		public CorLibTypeSig String {
			get { return module.CorLibTypes.String; }
		}

		public CorLibTypeSig TypedReference {
			get { return module.CorLibTypes.TypedReference; }
		}

		public ClassSig type(string ns, string name, string asmSimpleName) {
			return type(ns, name, findAssemblyRef(asmSimpleName));
		}

		public ClassSig type(string ns, string name) {
			return type(ns, name, CorLib);
		}

		public ClassSig type(string ns, string name, AssemblyRef asmRef) {
			return (ClassSig)type(false, ns, name, asmRef);
		}

		public ValueTypeSig valueType(string ns, string name, string asmSimpleName) {
			return valueType(ns, name, findAssemblyRef(asmSimpleName));
		}

		public ValueTypeSig valueType(string ns, string name) {
			return valueType(ns, name, CorLib);
		}

		public ValueTypeSig valueType(string ns, string name, AssemblyRef asmRef) {
			return (ValueTypeSig)type(true, ns, name, asmRef);
		}

		public ClassOrValueTypeSig type(bool isValueType, string ns, string name, IResolutionScope resolutionScope) {
			var typeRef = module.UpdateRowId(new TypeRefUser(module, ns, name, resolutionScope));
			ClassOrValueTypeSig type;
			if (isValueType)
				type = new ValueTypeSig(typeRef);
			else
				type = new ClassSig(typeRef);
			return (ClassOrValueTypeSig)add(type);
		}

		public SZArraySig array(TypeSig typeRef) {
			return (SZArraySig)add(new SZArraySig(typeRef));
		}

		TypeSig add(TypeSig typeRef) {
			TypeSig createdTypeRef;
			if (createdTypes.TryGetValue(typeRef, out createdTypeRef)) {
				if (createdTypeRef.ElementType != typeRef.ElementType)
					throw new ApplicationException(string.Format("Type {0}'s IsValueType is not correct", createdTypeRef));
				return createdTypeRef;
			}
			createdTypes[typeRef] = typeRef;
			return typeRef;
		}

		public MemberRef instanceMethod(string name, IMemberRefParent declaringType, TypeSig returnType, params TypeSig[] args) {
			return method(true, name, declaringType, returnType, args);
		}

		public MemberRef staticMethod(string name, IMemberRefParent declaringType, TypeSig returnType, params TypeSig[] args) {
			return method(false, name, declaringType, returnType, args);
		}

		public MemberRef method(bool isInstance, string name, IMemberRefParent declaringType, TypeSig returnType, params TypeSig[] args) {
			MethodSig sig;
			if (isInstance)
				sig = MethodSig.CreateInstance(returnType, args);
			else
				sig = MethodSig.CreateStatic(returnType, args);
			return module.UpdateRowId(new MemberRefUser(module, name, sig, declaringType));
		}

		AssemblyRef findAssemblyRef(string asmSimpleName) {
			var asmRef = module.GetAssemblyRef(asmSimpleName);
			if (asmRef == null)
				throw new ApplicationException(string.Format("Could not find assembly {0} in assembly references", asmSimpleName));
			return asmRef;
		}
	}
}
