﻿/* Copyright 2013-2014 MongoDB Inc.
*
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Driver.Core.Bindings;
using MongoDB.Driver.Core.Misc;
using MongoDB.Driver.Core.WireProtocol.Messages.Encoders;

namespace MongoDB.Driver.Core.Operations
{
    public class FindOneAndDeleteOperation<TResult> : FindAndModifyOperationBase<TResult>
    {
        // fields
        private readonly BsonDocument _filter;
        private TimeSpan? _maxTime;
        private BsonDocument _projection;
        private BsonDocument _sort;

        // constructors
        public FindOneAndDeleteOperation(CollectionNamespace collectionNamespace, BsonDocument filter, IBsonSerializer<TResult> resultSerializer, MessageEncoderSettings messageEncoderSettings)
            : base(collectionNamespace, resultSerializer, messageEncoderSettings)
        {
            _filter = Ensure.IsNotNull(filter, "filter");
        }

        // properties
        public BsonDocument Filter
        {
            get { return _filter; }
        }

        public TimeSpan? MaxTime
        {
            get { return _maxTime; }
            set { _maxTime = value; }
        }

        public BsonDocument Projection
        {
            get { return _projection; }
            set { _projection = value; }
        }

        public BsonDocument Sort
        {
            get { return _sort; }
            set { _sort = value; }
        }

        // methods
        public override BsonDocument CreateCommand()
        {
            return new BsonDocument
            {
                { "findAndModify", CollectionNamespace.CollectionName },
                { "query", _filter },
                { "sort", _sort, _sort != null },
                { "remove", true },
                { "fields", _projection, _projection != null },
                { "maxTimeMS", () => _maxTime.Value.TotalMilliseconds, _maxTime.HasValue }
            };
        }

        protected override Bson.IO.IElementNameValidator GetCommandValidator()
        {
            return NoOpElementNameValidator.Instance;
        }
    }
}
