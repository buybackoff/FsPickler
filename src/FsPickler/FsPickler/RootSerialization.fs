﻿module internal Nessos.FsPickler.RootSerialization

    open System
    open System.Collections
    open System.Collections.Generic
    open System.Text
    open System.IO
    open System.Runtime.Serialization
    
    open Nessos.FsPickler
    open Nessos.FsPickler.ReflectionCache
    open Nessos.FsPickler.SequenceUtils

    let initStreamWriter (formatP : IPickleFormatProvider) stream encoding isSeq leaveOpen =
        let leaveOpen = defaultArg leaveOpen false
        let encoding = defaultArg encoding formatP.DefaultEncoding
        formatP.CreateWriter(stream, encoding, isSeq, leaveOpen)

    let initStreamReader (formatP : IPickleFormatProvider) stream encoding isSeq leaveOpen =
        let leaveOpen = defaultArg leaveOpen false
        let encoding = defaultArg encoding formatP.DefaultEncoding
        formatP.CreateReader(stream, encoding, isSeq, leaveOpen)

    let initTextWriter (formatP : ITextPickleFormatProvider) writer isSeq leaveOpen =
        let leaveOpen = defaultArg leaveOpen false
        formatP.CreateWriter(writer, isSeq, leaveOpen)

    let initTextReader (formatP : ITextPickleFormatProvider) reader isSeq leaveOpen =
        let leaveOpen = defaultArg leaveOpen false
        formatP.CreateReader(reader, isSeq, leaveOpen)

    let writeRootObject resolver reflectionCache formatter streamingContext (pickler : Pickler<'T>) (value : 'T) =
        let writeState = new WriteState(formatter, resolver, reflectionCache, ?streamingContext = streamingContext)
        let typeName = reflectionCache.GetTypeSignature pickler.Type
        formatter.BeginWriteRoot typeName
        pickler.Write writeState "value" value
        formatter.EndWriteRoot ()

    let readRootObject resolver reflectionCache formatter streamingContext (pickler : Pickler<'T>) =
        let readState = new ReadState(formatter, resolver, reflectionCache, ?streamingContext = streamingContext)
        let typeName = reflectionCache.GetTypeSignature pickler.Type

        try formatter.BeginReadRoot typeName
        with 
        | :? FsPicklerException -> reraise () 
        | e -> raise <| new InvalidPickleException("error reading from pickle.", e)

        let value = pickler.Read readState "value"
        formatter.EndReadRoot ()
        value

    let writeRootObjectUntyped resolver reflectionCache formatter streamingContext (pickler : Pickler) (value : obj) =
        let writeState = new WriteState(formatter, resolver, reflectionCache, ?streamingContext = streamingContext)
        let typeName = reflectionCache.GetTypeSignature pickler.Type
        formatter.BeginWriteRoot typeName
        pickler.UntypedWrite writeState "value" value
        formatter.EndWriteRoot ()

    let readRootObjectUntyped resolver reflectionCache formatter streamingContext (pickler : Pickler) =
        let readState = new ReadState(formatter, resolver, reflectionCache, ?streamingContext = streamingContext)
        let typeName = reflectionCache.GetTypeSignature pickler.Type
        try formatter.BeginReadRoot typeName
        with 
        | :? FsPicklerException -> reraise () 
        | e -> raise <| new InvalidPickleException("error reading from pickle.", e)

        let value = pickler.UntypedRead readState "value"
        formatter.EndReadRoot ()
        value

    //
    //  top-level sequence serialization
    //

    /// serializes a sequence of objects to stream

    let writeTopLevelSequence resolver reflectionCache formatter streamingContext (pickler : Pickler<'T>) (values : seq<'T>) : int =
        // write state initialization
        let state = new WriteState(formatter, resolver, reflectionCache, ?streamingContext = streamingContext)
        let typeName = reflectionCache.GetTypeSignature pickler.Type + " seq"

        state.Formatter.BeginWriteRoot typeName
        let length = writeUnboundedSequence pickler state "values" values
        state.Formatter.EndWriteRoot ()
        length

    let readTopLevelSequence resolver reflectionCache formatter streamingContext (pickler : Pickler<'T>) : seq<'T> =
        
        // read state initialization
        let state = new ReadState(formatter, resolver, reflectionCache, ?streamingContext = streamingContext)
        let typeName = reflectionCache.GetTypeSignature pickler.Type + " seq"

        // read stream header
        try formatter.BeginReadRoot typeName
        with 
        | :? FsPicklerException -> reraise () 
        | e -> raise <| new InvalidPickleException("error reading from pickle.", e)

        readUnboundedSequenceLazy pickler state "values"



    let writeTopLevelSequenceUntyped resolver reflectionCache formatter streamingContext (pickler : Pickler) (values : IEnumerable) : int =
        let unpacker =
            {
                new IPicklerUnpacker<int> with
                    member __.Apply (p : Pickler<'T>) =
                        writeTopLevelSequence resolver reflectionCache formatter
                            streamingContext p (values :?> IEnumerable<'T>)
            }

        pickler.Unpack unpacker

    let readTopLevelSequenceUntyped resolver reflectionCache formatter streamingContext (pickler : Pickler) : IEnumerable =
        let unpacker =
            {
                new IPicklerUnpacker<IEnumerable> with
                    member __.Apply (p : Pickler<'T>) =
                        readTopLevelSequence resolver reflectionCache formatter 
                            streamingContext p :> IEnumerable
            }

        pickler.Unpack unpacker