/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Reflection;
using NUnit.Framework;
using NUnit.Framework.SyntaxHelpers;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Tests.Common;
using OpenSim.Tests.Common.Mock;
using OpenSim.Tests.Common.Setup;

namespace OpenSim.Region.Framework.Scenes.Tests
{
    [TestFixture]
    public class SceneGraphTests
    {
        [Test]
        public void TestDuplicateObject()
        {
            TestHelper.InMethod();
            Scene scene = SceneSetupHelpers.SetupScene();

            UUID ownerUuid = new UUID("00000000-0000-0000-0000-000000000010");
            string objName = "obj1";
            UUID objUuid = new UUID("00000000-0000-0000-0000-000000000001");

            SceneObjectPart part
                = new SceneObjectPart(ownerUuid, PrimitiveBaseShape.Default, Vector3.Zero, Quaternion.Identity, Vector3.Zero) 
                    { Name = objName, UUID = objUuid };

            scene.AddNewSceneObject(new SceneObjectGroup(part), false);
            
            SceneObjectGroup duplicatedSo 
                = scene.SceneGraph.DuplicateObject(
                    part.LocalId, new Vector3(10, 0, 0), 0, ownerUuid, UUID.Zero, Quaternion.Identity);
            
            Assert.That(duplicatedSo.Children.Count, Is.EqualTo(1));
            Assert.That(duplicatedSo.RootPart.LocalId, Is.Not.EqualTo(part.LocalId));
            
            //SceneObjectPart retrievedPart = scene.GetSceneObjectPart(objUuid);
        }        
    }
}