namespace Avalanchain.Cloud

open ChunkedCloudStream
open MBrace.Core
open System

module ChainFlow =

    let inline ofSink<'T> maxBatchSize = streamOfSink<'T> maxBatchSize

    let inline ofStream (stream: CloudStream<'T>) : Cloud<CloudStream<'T>> = cloud { return stream }

    let inline ofArray chunkSize (source: 'T[]) : Cloud<CloudStream<'T>> = cloud {
        let! (sink, sr) = streamOfSink chunkSize
        do! sink.PushBatch source
        return sr          
    }

    let inline ofQueue chunkSize (queue: CloudQueue<'T>) : Cloud<CloudStream<'T>> = 
        streamOfQueue<'T> queue chunkSize          

    let inline filter chunkSize (predicate: 'TD -> bool) (cloudStream: Cloud<CloudStream<'TD>>) : Cloud<CloudStream<'TD>> = cloud {
        let! stream = cloudStream
        return! streamOfStreamFold<'TD, 'TD> stream None chunkSize (fun df -> predicate df.Value) (fun t d -> d.Value)
    }

    let inline filterFrame chunkSize (predicate: IStreamFrame<'TD> -> bool) (cloudStream: Cloud<CloudStream<'TD>>) : Cloud<CloudStream<'TD>> = cloud {
        let! stream = cloudStream
        return! streamOfStreamFold<'TD, 'TD> stream None chunkSize predicate (fun t d -> d.Value)
    }

    let inline map chunkSize (mapF: 'TD -> 'TS) (cloudStream: Cloud<CloudStream<'TD>>) : Cloud<CloudStream<'TS>> = cloud {
        let! stream = cloudStream
        return! streamOfStreamFold<'TS, 'TD> stream None chunkSize (fun _ -> true) (fun _ d -> mapF d.Value)
    }

    let inline mapFrame chunkSize (mapF: IStreamFrame<'TD> -> 'TS) (cloudStream: Cloud<CloudStream<'TD>>) : Cloud<CloudStream<'TS>> = cloud {
        let! stream = cloudStream
        return! streamOfStreamFold<'TS, 'TD> stream None chunkSize (fun _ -> true) (fun _ d -> mapF d)
    }

    let inline fold chunkSize (foldF: 'TS -> 'TD -> 'TS) (state: 'TS) (cloudStream: Cloud<CloudStream<'TD>>) = cloud {
        let! stream = cloudStream
        return! streamOfStreamFold<'TS, 'TD> stream (Some state) chunkSize (fun _ -> true) (fun t d -> foldF (match t with None -> state | Some v -> v) d.Value)
    }

    let inline reduce chunkSize (reducer: 'T -> 'T -> 'T) (cloudStream: Cloud<CloudStream<'T>>) : Cloud<CloudStream<'T>> 
                when 'T : (static member Zero : 'T) = 
        fold chunkSize reducer LanguagePrimitives.GenericZero cloudStream

    let inline sum chunkSize (cloudStream: Cloud<CloudStream<'T>>) : Cloud<CloudStream<'T>> 
            when 'T : (static member Zero : 'T)
            and 'T : (static member (+) : 'T * 'T -> 'T) =
        fold chunkSize (+) LanguagePrimitives.GenericZero cloudStream

    let inline toArray (cloudStream: Cloud<CloudStream<'T>>) : Cloud<'T []> = cloud { // TODO: add toObservable
        let! stream = cloudStream
        return! stream.GetPage 0UL UInt32.MaxValue
    }

    let inline toEverywhere chunkSize initialValue (preFilter: IStreamFrame<'TD> -> bool) (foldF: 'TS option -> IStreamFrame<'TD> -> 'TS) (cloudStream: Cloud<CloudStream<'TD>>) = cloud {
        let! stream = cloudStream
        return! everywhereStream<'TS, 'TD> stream initialValue chunkSize preFilter foldF
    }

    let inline filterEverywhere chunkSize (predicate: 'TD -> bool) (cloudStream: Cloud<CloudStream<'TD>>) : Cloud<CloudStream<'TD>[]> = cloud {
        let! stream = cloudStream
        return! everywhereStream<'TD, 'TD> stream None chunkSize (fun df -> predicate df.Value) (fun t d -> d.Value)
    }

    let inline filterFrameEverywhere chunkSize (predicate: IStreamFrame<'TD> -> bool) (cloudStream: Cloud<CloudStream<'TD>>) : Cloud<CloudStream<'TD>[]> = cloud {
        let! stream = cloudStream
        return! everywhereStream<'TD, 'TD> stream None chunkSize predicate (fun t d -> d.Value)
    }

    let inline mapEverywhere chunkSize (mapF: 'TD -> 'TS) (cloudStream: Cloud<CloudStream<'TD>>) : Cloud<CloudStream<'TS>[]> = cloud {
        let! stream = cloudStream
        return! everywhereStream<'TS, 'TD> stream None chunkSize (fun _ -> true) (fun _ d -> mapF d.Value)
    }

    let inline mapFrameEverywhere chunkSize (mapF: IStreamFrame<'TD> -> 'TS) (cloudStream: Cloud<CloudStream<'TD>>) : Cloud<CloudStream<'TS>[]> = cloud {
        let! stream = cloudStream
        return! everywhereStream<'TS, 'TD> stream None chunkSize (fun _ -> true) (fun _ d -> mapF d)
    }

    let inline foldEverywhere chunkSize (foldF: 'TS -> 'TD -> 'TS) (state: 'TS) (cloudStream: Cloud<CloudStream<'TD>>) = cloud {
        let! stream = cloudStream
        return! everywhereStream<'TS, 'TD> stream (Some state) chunkSize (fun _ -> true) (fun t d -> foldF (match t with None -> state | Some v -> v) d.Value)
    }

    let inline reduceEverywhere chunkSize (reducer: 'T -> 'T -> 'T) (cloudStream: Cloud<CloudStream<'T>>) : Cloud<CloudStream<'T>[]> 
                when 'T : (static member Zero : 'T) = 
        foldEverywhere chunkSize reducer LanguagePrimitives.GenericZero cloudStream

    let inline sumEverywhere chunkSize (cloudStream: Cloud<CloudStream<'T>>) : Cloud<CloudStream<'T>[]> 
            when 'T : (static member Zero : 'T)
            and 'T : (static member (+) : 'T * 'T -> 'T) =
        foldEverywhere chunkSize (+) LanguagePrimitives.GenericZero cloudStream