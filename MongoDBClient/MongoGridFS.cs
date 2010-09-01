﻿/* Copyright 2010 10gen Inc.
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
using System.IO;
using System.Text;

using MongoDB.BsonLibrary;

namespace MongoDB.MongoDBClient {
    public class MongoGridFS {
        #region private fields
        private MongoDatabase database;
        private MongoGridFSSettings settings;
        private MongoCollection chunksCollection;
        private MongoCollection filesCollection;
        private SafeMode safeMode;
        #endregion

        #region constructors
        public MongoGridFS(
            MongoDatabase database,
            MongoGridFSSettings settings
        ) {
            this.database = database;
            this.settings = settings;
            this.safeMode = database.SafeMode;
        }
        #endregion

        #region public properties
        public MongoCollection ChunksCollection {
            get {
                if (chunksCollection == null || chunksCollection.Name != settings.ChunksCollectionName) {
                    chunksCollection = database.GetCollection(settings.ChunksCollectionName);
                }
                return chunksCollection;
            }
        }

        public MongoCollection FilesCollection {
            get {
                if (filesCollection == null || filesCollection.Name != settings.FilesCollectionName) {
                    filesCollection = database.GetCollection(settings.FilesCollectionName);
                }
                return filesCollection;
            }
        }

        public SafeMode SafeMode {
            get { return safeMode; }
            set { safeMode = value; }
        }

        public MongoGridFSSettings Settings {
            get { return settings; }
            set { settings = value; }
        }
        #endregion

        #region public methods
        public void Delete(
            BsonDocument query
        ) {
            var fileIds = FilesCollection.Find<BsonDocument>(query).Select(f => f.GetObjectId("_id"));
            foreach (var fileId in fileIds) {
                var fileQuery = new BsonDocument("_id", fileId);
                FilesCollection.Remove(fileQuery, safeMode);
                var chunksQuery = new BsonDocument("files_id", fileId);
                ChunksCollection.Remove(chunksQuery, safeMode);
            }
        }

        public void Delete(
            BsonObjectId id
        ) {
            var query = new BsonDocument("_id", id);
            Delete(query);
        }

        public void Delete(
            string remoteFileName
        ) {
            var query = new BsonDocument("filename", remoteFileName);
            Delete(query);
        }

        public void Download(
            Stream stream,
            BsonDocument query
        ) {
            Download(stream, query, -1); // most recent version
        }

        public void Download(
            Stream stream,
            BsonDocument query,
            int version
        ) {
            var fileInfo = FindOne(query, version);
            if (fileInfo == null) {
                string errorMessage = string.Format("GridFS file not found: {0}", query);
                throw new MongoException(errorMessage);
            }

            Download(stream, fileInfo);
        }

        public void Download(
            Stream stream,
            MongoGridFSFileInfo fileInfo
        ) {
            var numberOfChunks = (fileInfo.Length + fileInfo.ChunkSize - 1) / fileInfo.ChunkSize;
            for (int n = 0; n < numberOfChunks; n++) {
                var query = new BsonDocument {
                    { "files_id", fileInfo.Id },
                    { "n", n }
                };
                var chunk = ChunksCollection.FindOne<BsonDocument>(query);
                if (chunk == null) {
                    string errorMessage = string.Format("Chunk {0} missing for: {1}", n, fileInfo.Name);
                    throw new MongoException(errorMessage);
                }
                var data = chunk.GetBinaryData("data");
                if (data.Bytes.Length != fileInfo.ChunkSize) {
                    // the last chunk only has as many bytes as needed to complete the file
                    if (n < numberOfChunks - 1 || data.Bytes.Length != fileInfo.Length % fileInfo.ChunkSize) {
                        string errorMessage = string.Format("Chunk {0} for {1} is the wrong size", n, fileInfo.Name);
                        throw new MongoException(errorMessage);
                    }
                }
                stream.Write(data.Bytes, 0, data.Bytes.Length);
            }
        }

        public void Download(
            Stream stream,
            string remoteFileName
        ) {
            Download(stream, remoteFileName, -1); // most recent version
        }

        public void Download(
            Stream stream,
            string remoteFileName,
            int version
        ) {
            var query = new BsonDocument("filename", remoteFileName);
            Download(stream, query, version);
        }

        public void Download(
            string fileName
        ) {
            Download(fileName, -1); // most recent version
        }

        public void Download(
            string fileName,
            int version
        ) {
            Download(fileName, fileName, version); // same local and remote file names
        }

        public void Download(
            string localFileName,
            BsonDocument query
        ) {
            Download(localFileName, query, -1); // most recent version
        }

        public void Download(
            string localFileName,
            BsonDocument query,
            int version
        ) {
            using (Stream stream = File.Create(localFileName)) {
                Download(stream, query, version);
            }
        }

        public void Download(
            string localFileName,
            MongoGridFSFileInfo fileInfo
        ) {
            using (Stream stream = File.Create(localFileName)) {
                Download(stream, fileInfo);
            }
        }

        public void Download(
            string localFileName,
            string remoteFileName
        ) {
            Download(localFileName, remoteFileName, -1); // most recent version
        }

        public void Download(
            string localFileName,
            string remoteFileName,
            int version
        ) {
            using (Stream stream = File.Create(localFileName)) {
                Download(stream, remoteFileName, version);
            }
        }

        public bool Exists(
            BsonDocument query
        ) {
            return FilesCollection.Count(query) > 0;
        }

        public bool Exists(
            BsonObjectId id
        ) {
            var query = new BsonDocument("_id", id);
            return Exists(query);
        }

        public bool Exists(
            string fileName
        ) {
            var query = new BsonDocument("filename", fileName);
            return Exists(query);
        }

        public List<MongoGridFSFileInfo> Find() {
            return Find((BsonDocument) null);
        }

        public List<MongoGridFSFileInfo> Find(
            BsonDocument query
        ) {
            return FilesCollection.Find<BsonDocument>(query).Select(d => new MongoGridFSFileInfo(this, d)).ToList();
        }

        public List<MongoGridFSFileInfo> Find(
            string fileName
        ) {
            var query = new BsonDocument("filename", fileName);
            return Find(query);
        }

        public MongoGridFSFileInfo FindOne(
            BsonDocument query
        ) {
            return FindOne(query, -1); // most recent version
        }

        public MongoGridFSFileInfo FindOne(
            BsonDocument query,
            int version // 1 is oldest, -1 is newest, 0 is no sort
        ) {
            BsonDocument fileInfoDocument;
            if (version > 0) {
                fileInfoDocument = FilesCollection.Find<BsonDocument>(query).Sort("uploadDate").Skip(version - 1).Limit(1).FirstOrDefault();
            } else if (version < 0) {
                fileInfoDocument = FilesCollection.Find<BsonDocument>(query).Sort(new BsonDocument("uploadDate", -1)).Skip(-version - 1).Limit(1).FirstOrDefault();
            } else {
                fileInfoDocument = FilesCollection.FindOne<BsonDocument>(query);
            }
            return new MongoGridFSFileInfo(this, fileInfoDocument);
        }

        public MongoGridFSFileInfo FindOne(
            BsonObjectId id
        ) {
            var query = new BsonDocument("_id", id);
            return FindOne(query);
        }

        public MongoGridFSFileInfo FindOne(
            string remoteFileName
        ) {
            return FindOne(remoteFileName, -1); // most recent version
        }

        public MongoGridFSFileInfo FindOne(
            string remoteFileName,
            int version
        ) {
            var query = new BsonDocument("filename", remoteFileName);
            return FindOne(query, version);
        }

        public MongoGridFSFileInfo Upload(
            Stream stream,
            string remoteFileName
        ) {
            ChunksCollection.EnsureIndex("files_id", "n");

            BsonObjectId files_id = BsonObjectId.GenerateNewId();
            var chunkSize = settings.DefaultChunkSize;
            var buffer = new byte[chunkSize];

            var length = 0;
            for (int n = 0; true; n++) {
                int bytesRead = stream.Read(buffer, 0, chunkSize);
                if (bytesRead == 0) {
                    break;
                }
                length += bytesRead;

                byte[] data = buffer;
                if (bytesRead < chunkSize) {
                    data = new byte[bytesRead];
                    Buffer.BlockCopy(buffer, 0, data, 0, bytesRead);
                }

                var chunk = new BsonDocument {
                    { "_id", BsonObjectId.GenerateNewId() },
                    { "files_id", files_id },
                    { "n", n },
                    { "data", new BsonBinaryData(data) }
                };
                ChunksCollection.Insert(chunk, safeMode);

                if (bytesRead < chunkSize) {
                    break;
                }
            }

            var md5Command = new BsonDocument {
                { "filemd5", files_id },
                { "root", settings.Root }
            };
            var md5Result = database.RunCommand(md5Command);
            var md5 = md5Result.GetString("md5");

            BsonDocument fileInfo = new BsonDocument {
                { "_id", files_id },
                { "filename", remoteFileName },
                { "length", length },
                { "chunkSize", chunkSize },
                { "uploadDate", DateTime.UtcNow },
                { "md5", md5 }
            };
            FilesCollection.Insert(fileInfo, safeMode);

            return FindOne(files_id);
        }

        public MongoGridFSFileInfo Upload(
            string fileName
        ) {
            return Upload(fileName, fileName);
        }

        public MongoGridFSFileInfo Upload(
            string localFileName,
            string remoteFileName
        ) {
            using (Stream stream = File.OpenRead(localFileName)) {
                return Upload(stream, remoteFileName);
            }
        }
        #endregion
    }
}
