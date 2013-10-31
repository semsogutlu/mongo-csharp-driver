﻿/* Copyright 2010-2013 10gen Inc.
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
using MongoDB.Bson;

namespace MongoDB.Driver
{
    /// <summary>
    /// A mapper from error responses to custom exceptions.
    /// </summary>
    public static class ExceptionMapper
    {
        /// <summary>
        /// Maps the specified response to a custom exception (if possible).
        /// </summary>
        /// <param name="response">The response.</param>
        /// <returns>The custom exception (or null if the response could not be mapped to a custom exception).</returns>
        public static Exception Map(BsonDocument response)
        {
            BsonValue code;
            if (response.TryGetValue("code", out code) && code.IsNumeric)
            {
                switch (code.ToInt32())
                {
                    case 50:
                    case 13475:
                    case 16986:
                    case 16712:
                        return new ExecutionTimeoutException("Operation exceeded time limit.");
                }
            }

            // the server sometimes sends a response that is missing the "code" field but does have an "errmsg" field
            BsonValue errmsg;
            if (response.TryGetValue("errmsg", out errmsg) && errmsg.IsString)
            {
                if (errmsg.AsString.Contains("exceeded time limit") ||
                    errmsg.AsString.Contains("execution terminated"))
                {
                    return new ExecutionTimeoutException("Operation exceeded time limit.");
                }
            }

            return null;
        }

        /// <summary>
        /// Maps the specified writeConcernResult to a custom exception (if necessary).
        /// </summary>
        /// <param name="writeConcernResult">The write concern result.</param>
        /// <returns>
        /// The custom exception (or null if the writeConcernResult was not mapped to an exception).
        /// </returns>
        public static Exception Map(WriteConcernResult writeConcernResult)
        {
            if (writeConcernResult.Code.HasValue)
            {
                switch(writeConcernResult.Code.Value)
                {
                    case 11000:
                    case 11001:
                    case 12582:
                        var errorMessage = string.Format(
                            "WriteConcern detected an error '{0}'. (Response was {1}).",
                            writeConcernResult.ErrorMessage, writeConcernResult.Response.ToJson());
                        return new MongoDuplicateKeyException(errorMessage, writeConcernResult);
                }
            }

            if (!writeConcernResult.Ok)
            {
                var errorMessage = string.Format(
                    "WriteConcern detected an error '{0}'. (Response was {1}).",
                    writeConcernResult.ErrorMessage, writeConcernResult.Response.ToJson());
                return new WriteConcernException(errorMessage, writeConcernResult);
            }

            if (writeConcernResult.HasLastErrorMessage)
            {
                var errorMessage = string.Format(
                    "WriteConcern detected an error '{0}'. (Response was {1}).",
                    writeConcernResult.LastErrorMessage, writeConcernResult.Response.ToJson());
                return new WriteConcernException(errorMessage, writeConcernResult);
            }

            return null;
        }
    }
}