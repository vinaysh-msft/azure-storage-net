﻿// -----------------------------------------------------------------------------------------
// <copyright file="ExceptionHResultTest.cs" company="Microsoft">
//    Copyright 2013 Microsoft Corporation
// 
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
// 
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
// </copyright>
// -----------------------------------------------------------------------------------------

using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
using Microsoft.WindowsAzure.Storage.Core.Util;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.WindowsAzure.Storage.Blob
{
    [TestClass]
    public class ExceptionHResultTest : TestBase
    {
        private readonly CloudBlobClient DefaultBlobClient = new CloudBlobClient(new Uri(TestBase.TargetTenantConfig.BlobServiceEndpoint), TestBase.StorageCredentials);

        //
        // Use TestInitialize to run code before running each test 
        [TestInitialize()]
        public void MyTestInitialize()
        {
            if (TestBase.BlobBufferManager != null)
            {
                TestBase.BlobBufferManager.OutstandingBufferCount = 0;
            }
        }
        //
        // Use TestCleanup to run code after each test has run
        [TestCleanup()]
        public void MyTestCleanup()
        {
            if (TestBase.BlobBufferManager != null)
            {
                Assert.AreEqual(0, TestBase.BlobBufferManager.OutstandingBufferCount);
            }
        }

        [TestMethod]
        [TestCategory(ComponentCategory.Blob)]
        [TestCategory(TestTypeCategory.UnitTest)]
        [TestCategory(SmokeTestCategory.NonSmoke)]
        [TestCategory(TenantTypeCategory.DevStore), TestCategory(TenantTypeCategory.DevFabric), TestCategory(TenantTypeCategory.Cloud)]
        public async Task CloudContainerCreateNegativeConflictAsync()
        {
            try
            {
                string name = "abc";
                CloudBlobContainer container = DefaultBlobClient.GetContainerReference(name);
                await container.CreateAsync();
                await container.CreateAsync();
                Assert.Fail();
            }
            catch (Exception e)
            {
                Assert.AreEqual(WindowsAzureErrorCode.HttpConflict, e.HResult);
            }
        }

        [TestMethod]
        [TestCategory(ComponentCategory.Blob)]
        [TestCategory(TestTypeCategory.UnitTest)]
        [TestCategory(SmokeTestCategory.NonSmoke)]
        [TestCategory(TenantTypeCategory.DevStore), TestCategory(TenantTypeCategory.DevFabric), TestCategory(TenantTypeCategory.Cloud)]
        public async Task CloudBlobUploadTimeoutAsync()
        {
            CloudBlobContainer container = DefaultBlobClient.GetContainerReference(Guid.NewGuid().ToString("N"));
            byte[] buffer = BlobTestBase.GetRandomBuffer(4 * 1024 * 1024);

            try
            {
                await container.CreateAsync();

                CloudBlockBlob blob = container.GetBlockBlobReference("blob1");
                BlobRequestOptions requestOptions = new BlobRequestOptions()
                {
                    MaximumExecutionTime = TimeSpan.FromSeconds(1),
                    RetryPolicy = new NoRetry()
                };

                using (MemoryStream ms = new MemoryStream(buffer))
                {
                    await blob.UploadFromStreamAsync(ms.AsInputStream(), null, requestOptions, null);
                }

                Assert.Fail();
            }
            catch (Exception e)
            {
                Assert.AreEqual(WindowsAzureErrorCode.HttpRequestTimeout, e.HResult);
            }
            finally
            {
                container.DeleteIfExistsAsync().AsTask().Wait();
            }
        }

        [TestMethod]
        [TestCategory(ComponentCategory.Blob)]
        [TestCategory(TestTypeCategory.UnitTest)]
        [TestCategory(SmokeTestCategory.NonSmoke)]
        [TestCategory(TenantTypeCategory.DevFabric), TestCategory(TenantTypeCategory.Cloud)]
        public async Task CloudBlobUploadCancellationAsync()
        {
            CloudBlobContainer container = DefaultBlobClient.GetContainerReference(Guid.NewGuid().ToString("N"));
            byte[] buffer = BlobTestBase.GetRandomBuffer(4 * 1024 * 1024);

            try
            {
                await container.CreateAsync();

                CloudBlockBlob blob = container.GetBlockBlobReference("blob1");
                BlobRequestOptions requestOptions = new BlobRequestOptions()
                {
                    RetryPolicy = new NoRetry()
                };

                CancellationTokenSource cts = new CancellationTokenSource();
                CancellationToken token = cts.Token; 

                new Task(() =>
                {
                    new System.Threading.ManualResetEvent(false).WaitOne(500);
                    cts.Cancel(false);
                }).Start();

                using (MemoryStream ms = new MemoryStream(buffer))
                {
                    blob.UploadFromStreamAsync(ms.AsInputStream(), null, requestOptions, null).AsTask(token).Wait();
                }

                Assert.Fail();
            }
            catch (AggregateException e)
            {
                TaskCanceledException ex = new TaskCanceledException();
                Assert.AreEqual(ex.HResult, e.InnerException.HResult);
            }
            finally
            {
                container.DeleteIfExistsAsync().AsTask().Wait();
            }
        }
    }
}
