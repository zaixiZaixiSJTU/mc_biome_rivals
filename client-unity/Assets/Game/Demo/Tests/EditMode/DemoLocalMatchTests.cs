using BiomeRivals.Content;
using BiomeRivals.Core;
using BiomeRivals.Demo.Editor;
using NUnit.Framework;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BiomeRivals.Demo.Tests
{
    public sealed class DemoLocalMatchTests
    {
        [Test]
        public void LocalDemoSupportsDeployCastAndTurnLoop()
        {
            var registry = CardContentLoader.Load();
            var match = new DemoLocalMatch();
            match.ResetHand(new[] { "pf_001", "pf_005", "tk_016" });

            Assert.That(registry.TryGetDefinition("pf_001", out var bee), Is.True);
            Assert.That(match.TryDeploy(bee, DemoSlotKind.Unit, 0, out _), Is.True);
            Assert.That(match.UnitSlots[0], Is.EqualTo("pf_001"));
            Assert.That(match.Energy, Is.EqualTo(5));

            Assert.That(registry.TryGetDefinition("pf_005", out var nursery), Is.True);
            Assert.That(match.TryDeploy(nursery, DemoSlotKind.Building, 0, out _), Is.True);
            Assert.That(match.BuildingSlots[0], Is.EqualTo("pf_005"));
            Assert.That(match.Energy, Is.EqualTo(3));

            Assert.That(registry.TryGetDefinition("tk_016", out var shell), Is.True);
            Assert.That(match.TryCast(shell, out var castMessage), Is.True);
            Assert.That(castMessage, Does.Contain("2 点护甲"));
            Assert.That(match.Energy, Is.EqualTo(2));
            Assert.That(match.PlayerArmor, Is.EqualTo(2));
            Assert.That(match.DiscardPile, Does.Contain("tk_016"));

            match.EndPlayerTurn();
            Assert.That(match.IsPlayerTurn, Is.False);
            match.BeginNextPlayerTurn();
            Assert.That(match.IsPlayerTurn, Is.True);
            Assert.That(match.Round, Is.EqualTo(2));
            Assert.That(match.Energy, Is.EqualTo(7));
        }

        [Test]
        public void LocalTurnStartDrawsBurnsAndAppliesEscalatingFatigue()
        {
            var drawing = new DemoLocalMatch();
            drawing.ResetDeckAndHand(new[] { "pf_001" }, new[] { "pf_002", "pf_003" });
            drawing.EndPlayerTurn();
            var draw = drawing.BeginNextPlayerTurn();
            Assert.That(draw.Outcome, Is.EqualTo(DemoDrawOutcome.Drawn));
            Assert.That(draw.CardId, Is.EqualTo("pf_003"));
            Assert.That(drawing.Hand, Does.Contain("pf_003"));
            Assert.That(drawing.Deck, Has.Count.EqualTo(1));

            var burning = new DemoLocalMatch();
            burning.ResetDeckAndHand(
                new[] { "pf_001", "pf_002", "pf_003", "pf_004", "pf_005", "pf_006", "pf_007" },
                new[] { "pf_008" });
            burning.EndPlayerTurn();
            var burn = burning.BeginNextPlayerTurn();
            Assert.That(burn.Outcome, Is.EqualTo(DemoDrawOutcome.Burned));
            Assert.That(burning.Hand, Has.Count.EqualTo(7));
            Assert.That(burning.DiscardPile, Is.EqualTo(new[] { "pf_008" }));

            var fatiguing = new DemoLocalMatch();
            fatiguing.ResetDeckAndHand(new string[0], new string[0]);
            fatiguing.EndPlayerTurn();
            var firstFatigue = fatiguing.BeginNextPlayerTurn();
            fatiguing.EndPlayerTurn();
            var secondFatigue = fatiguing.BeginNextPlayerTurn();
            Assert.That(firstFatigue.FatigueDamage, Is.EqualTo(1));
            Assert.That(secondFatigue.FatigueDamage, Is.EqualTo(2));
            Assert.That(fatiguing.PlayerLife, Is.EqualTo(27));
            Assert.That(fatiguing.FatigueCount, Is.EqualTo(2));
        }

        [Test]
        public void LocalImplementedEffectsResolveAndPendingEffectsRemainUnspent()
        {
            var registry = CardContentLoader.Load();

            var sacrificeMatch = new DemoLocalMatch();
            sacrificeMatch.ResetDeckAndHand(new[] { "nt_006" }, new[] { "nt_001" });
            Assert.That(registry.TryGetDefinition("nt_006", out var sacrifice), Is.True);
            Assert.That(sacrifice.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));
            Assert.That(sacrificeMatch.TryCast(sacrifice, out var sacrificeMessage), Is.True);
            Assert.That(sacrificeMatch.PlayerLife, Is.EqualTo(28));
            Assert.That(sacrificeMatch.Hand, Is.EqualTo(new[] { "nt_001" }));
            Assert.That(sacrificeMessage, Does.Contain("真实伤害"));

            var fleshMatch = new DemoLocalMatch();
            fleshMatch.ResetDeckAndHand(new[] { "tk_005" }, new string[0]);
            Assert.That(registry.TryGetDefinition("tk_005", out var flesh), Is.True);
            Assert.That(fleshMatch.TryCast(flesh, out _), Is.True);
            Assert.That(fleshMatch.PlayerLife, Is.EqualTo(29));

            var pendingMatch = new DemoLocalMatch();
            pendingMatch.ResetDeckAndHand(new[] { "pf_006" }, new string[0]);
            Assert.That(registry.TryGetDefinition("pf_006", out var pending), Is.True);
            Assert.That(pendingMatch.TryCast(pending, out var pendingMessage), Is.False);
            Assert.That(pendingMatch.Hand, Does.Contain("pf_006"));
            Assert.That(pendingMatch.Energy, Is.EqualTo(6));
            Assert.That(pendingMessage, Does.Contain("尚未接入"));
        }

        [Test]
        public void LocalSuspiciousSandBurialExcavatesBeforeTheNormalDraw()
        {
            var registry = CardContentLoader.Load();
            var match = new DemoLocalMatch();
            match.ResetDeckAndHand(new[] { "db_002" }, new[] { "pf_001" });
            Assert.That(registry.TryGetDefinition("db_002", out var suspiciousSand), Is.True);
            Assert.That(suspiciousSand.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));

            Assert.That(match.TryCast(suspiciousSand, out var message), Is.True);
            Assert.That(message, Does.Contain("陶片"));
            Assert.That(match.BuriedCount, Is.EqualTo(1));
            Assert.That(match.Deck, Does.Contain("tk_006"));
            Assert.That(match.PlayerArmor, Is.EqualTo(1));

            match.EndPlayerTurn();
            var firstDraw = match.BeginNextPlayerTurn();
            var excavated = firstDraw.ExcavatedCardIds;
            if (excavated.Length == 0)
            {
                match.EndPlayerTurn();
                excavated = match.BeginNextPlayerTurn().ExcavatedCardIds;
            }

            Assert.That(excavated, Is.EqualTo(new[] { "tk_006" }));
            Assert.That(match.BuriedCount, Is.Zero);
            Assert.That(match.Hand, Does.Contain("tk_006"));
            Assert.That(match.PlayerArmor, Is.EqualTo(2));
            Assert.That(match.ExcavatedThisTurn, Is.True);
        }

        [Test]
        public void LocalBadlandsRaiderCostsOneLessOnlyDuringAnExcavationTurn()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("db_005", out var raider), Is.True);
            Assert.That(registry.TryGetDefinition("db_004", out var camel), Is.True);
            var match = new DemoLocalMatch();
            match.ResetDeckAndHand(new[] { raider.id }, new[] { "db_001", "tk_006" }, new[] { "tk_006" });

            Assert.That(match.GetEffectiveCost(raider), Is.EqualTo(3));
            Assert.That(match.GetEffectiveCost(camel), Is.EqualTo(camel.cost));

            match.EndPlayerTurn();
            var draw = match.BeginNextPlayerTurn();

            Assert.That(draw.ExcavatedCardIds, Is.EqualTo(new[] { "tk_006" }));
            Assert.That(match.ExcavatedThisTurn, Is.True);
            Assert.That(match.GetEffectiveCost(raider), Is.EqualTo(2));
            Assert.That(match.GetEffectiveCost(camel), Is.EqualTo(camel.cost));
            Assert.That(match.TryDeploy(raider, DemoSlotKind.Unit, 0, out _), Is.True);
            Assert.That(match.Energy, Is.EqualTo(5), "Turn two starts at seven energy and the discounted raider spends two.");

            match.EndPlayerTurn();
            Assert.That(match.ExcavatedThisTurn, Is.False);
            Assert.That(match.GetEffectiveCost(raider), Is.EqualTo(3));
        }

        [Test]
        public void LocalArchaeologistRequiresAChoiceAndExcavatesTheSelectedBuriedCard()
        {
            var registry = CardContentLoader.Load();
            var match = new DemoLocalMatch();
            match.ResetDeckAndHand(
                new[] { "db_003" },
                new[] { "db_004", "tk_006", "db_001" },
                new[] { "tk_006" });
            Assert.That(registry.TryGetDefinition("db_003", out var archaeologist), Is.True);
            Assert.That(archaeologist.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));

            var deployed = match.ApplyDeploy(archaeologist, match.CreateDeployCommand("db_003", DemoSlotKind.Unit, 0));

            Assert.That(deployed.Accepted, Is.True);
            Assert.That(match.PendingChoice, Is.Not.Null);
            Assert.That(match.PendingChoice.options.Select(option => option.cardId),
                Is.EqualTo(new[] { "db_001", "tk_006", "db_004" }));
            Assert.That(match.PendingChoice.options.Single(option => option.selectable).optionIndex, Is.EqualTo(1));
            var blocked = match.ApplyEnterCombat(match.CreateEnterCombatCommand());
            Assert.That(blocked.Accepted, Is.False);
            Assert.That(blocked.Code, Is.EqualTo(DemoCommandRejectionCode.ChoiceRequired));

            var revisionBeforeInvalidChoice = match.Revision;
            var invalid = match.ApplyResolveChoice(match.CreateResolveChoiceCommand(match.PendingChoice.choiceId, 0));
            Assert.That(invalid.Accepted, Is.False);
            Assert.That(invalid.Code, Is.EqualTo(DemoCommandRejectionCode.InvalidChoice));
            Assert.That(match.Revision, Is.EqualTo(revisionBeforeInvalidChoice));
            Assert.That(match.PendingChoice, Is.Not.Null);

            var resolved = match.ApplyResolveChoice(match.CreateResolveChoiceCommand(match.PendingChoice.choiceId, 1));
            Assert.That(resolved.Accepted, Is.True);
            Assert.That(match.PendingChoice, Is.Null);
            Assert.That(match.Hand, Is.EqualTo(new[] { "tk_006", "db_001" }));
            Assert.That(match.Deck, Is.EqualTo(new[] { "db_004" }));
            Assert.That(match.BuriedCount, Is.Zero);
            Assert.That(match.PlayerArmor, Is.EqualTo(1));
        }

        [Test]
        public void LocalArchaeologistExplicitlyConfirmsWhenInspectionFindsNoBuriedCard()
        {
            var registry = CardContentLoader.Load();
            var match = new DemoLocalMatch();
            match.ResetDeckAndHand(new[] { "db_003" }, new[] { "db_004", "db_001" });
            Assert.That(registry.TryGetDefinition("db_003", out var archaeologist), Is.True);
            Assert.That(match.TryDeploy(archaeologist, DemoSlotKind.Unit, 0, out _), Is.True);

            var choiceId = match.PendingChoice.choiceId;
            Assert.That(match.PendingChoice.options.All(option => !option.selectable), Is.True);
            var invalid = match.ApplyResolveChoice(match.CreateResolveChoiceCommand(choiceId, 0));
            Assert.That(invalid.Accepted, Is.False);
            var resolved = match.ApplyResolveChoice(match.CreateResolveChoiceCommand(choiceId, -1));

            Assert.That(resolved.Accepted, Is.True);
            Assert.That(match.PendingChoice, Is.Null);
            Assert.That(match.Hand, Is.Empty);
            Assert.That(match.Deck, Is.EqualTo(new[] { "db_004", "db_001" }));
        }

        [Test]
        public void TargetedSnowballUsesStableInstanceAndExpiresAtEndOfTurn()
        {
            var registry = CardContentLoader.Load();
            var match = new DemoLocalMatch();
            match.ResetDeckAndHand(new[] { "si_001" }, new string[0]);
            Assert.That(registry.TryGetDefinition("si_001", out var snowball), Is.True);
            Assert.That(registry.TryGetDefinition("nt_003", out var blaze), Is.True);
            match.ResetOpponent(new[] { blaze });
            var target = match.GetObject(false, DemoSlotKind.Unit, 0);
            Assert.That(target.InstanceId, Does.Match("^object-[0-9]+$"));

            var missingTarget = match.ApplyPlayCard(snowball, match.CreatePlayCardCommand("si_001"));
            Assert.That(missingTarget.Accepted, Is.False);
            Assert.That(missingTarget.Code, Is.EqualTo(DemoCommandRejectionCode.InvalidTarget));
            Assert.That(match.Hand, Does.Contain("si_001"));

            var played = match.ApplyPlayCard(snowball, match.CreatePlayCardCommand("si_001", "UNIT", target.InstanceId));
            Assert.That(played.Accepted, Is.True);
            Assert.That(target.Attack, Is.EqualTo(2));
            Assert.That(target.TemporaryAttackModifier, Is.EqualTo(-1));
            Assert.That(match.DiscardPile, Does.Contain("si_001"));

            match.EndPlayerTurn();
            Assert.That(target.Attack, Is.EqualTo(3));
            Assert.That(target.TemporaryAttackModifier, Is.Zero);
        }

        [Test]
        public void ExpandedImplementedEffectsResolveInLocalParityMode()
        {
            var registry = CardContentLoader.Load();

            var beeMatch = new DemoLocalMatch();
            beeMatch.ResetDeckAndHand(new[] { "nt_006", "pf_001" }, new[] { "nt_001" });
            Assert.That(registry.TryGetDefinition("nt_006", out var sacrifice), Is.True);
            Assert.That(registry.TryGetDefinition("pf_001", out var bee), Is.True);
            Assert.That(beeMatch.TryCast(sacrifice, out _), Is.True);
            Assert.That(beeMatch.PlayerLife, Is.EqualTo(28));
            Assert.That(beeMatch.TryDeploy(bee, DemoSlotKind.Unit, 0, out var beeMessage), Is.True);
            Assert.That(beeMatch.PlayerLife, Is.EqualTo(29));
            Assert.That(beeMessage, Does.Contain("战吼"));

            var boneMatch = new DemoLocalMatch();
            boneMatch.ResetHand(new[] { "pf_001", "tk_009" });
            Assert.That(registry.TryGetDefinition("tk_009", out var bone), Is.True);
            Assert.That(boneMatch.TryDeploy(bee, DemoSlotKind.Unit, 0, out _), Is.True);
            var friendlyUnit = boneMatch.GetObject(true, DemoSlotKind.Unit, 0);
            var boneResult = boneMatch.ApplyPlayCard(bone,
                boneMatch.CreatePlayCardCommand("tk_009", "UNIT", friendlyUnit.InstanceId));
            Assert.That(boneResult.Accepted, Is.True);
            Assert.That(friendlyUnit.Attack, Is.EqualTo(2));
            Assert.That(friendlyUnit.TemporaryAttackModifier, Is.EqualTo(1));
            boneMatch.EndPlayerTurn();
            Assert.That(friendlyUnit.Attack, Is.EqualTo(1));

            var cobbleMatch = new DemoLocalMatch();
            cobbleMatch.ResetHand(new[] { "db_004", "tk_010" });
            Assert.That(registry.TryGetDefinition("db_004", out var fence), Is.True);
            Assert.That(registry.TryGetDefinition("tk_010", out var cobblestone), Is.True);
            Assert.That(cobbleMatch.TryDeploy(fence, DemoSlotKind.Building, 0, out _), Is.True);
            var friendlyBuilding = cobbleMatch.GetObject(true, DemoSlotKind.Building, 0);
            friendlyBuilding.Health = 1;
            var cobbleResult = cobbleMatch.ApplyPlayCard(cobblestone,
                cobbleMatch.CreatePlayCardCommand("tk_010", "BUILDING", friendlyBuilding.InstanceId));
            Assert.That(cobbleResult.Accepted, Is.True);
            Assert.That(friendlyBuilding.Health, Is.EqualTo(3));

            var stormMatch = new DemoLocalMatch();
            stormMatch.ResetHand(new[] { "pf_001", "db_006" });
            Assert.That(registry.TryGetDefinition("db_006", out var sandstorm), Is.True);
            Assert.That(registry.TryGetDefinition("nt_003", out var blaze), Is.True);
            Assert.That(stormMatch.TryDeploy(bee, DemoSlotKind.Unit, 0, out _), Is.True);
            stormMatch.ResetOpponent(new[] { blaze });
            Assert.That(stormMatch.TryCast(sandstorm, out var stormMessage), Is.True);
            Assert.That(stormMatch.GetObject(true, DemoSlotKind.Unit, 0), Is.Null);
            Assert.That(stormMatch.GetObject(false, DemoSlotKind.Unit, 0).Health, Is.EqualTo(1));
            Assert.That(stormMessage, Does.Contain("沙尘暴"));
        }

        [Test]
        public void TargetRegistrySeparatesFriendlyEnemyAndBuildingTargets()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("si_001", out var snowball), Is.True);
            Assert.That(registry.TryGetDefinition("tk_009", out var bone), Is.True);
            Assert.That(registry.TryGetDefinition("tk_010", out var cobblestone), Is.True);

            Assert.That(DemoCardTargeting.TryGetRule(snowball, out var snowballRule), Is.True);
            Assert.That(snowballRule.Owner, Is.EqualTo(DemoTargetOwner.Enemy));
            Assert.That(snowballRule.SlotKind, Is.EqualTo(DemoSlotKind.Unit));
            Assert.That(DemoCardTargeting.TryGetRule(bone, out var boneRule), Is.True);
            Assert.That(boneRule.Owner, Is.EqualTo(DemoTargetOwner.Friendly));
            Assert.That(boneRule.TargetType, Is.EqualTo("UNIT"));
            Assert.That(DemoCardTargeting.TryGetRule(cobblestone, out var cobbleRule), Is.True);
            Assert.That(cobbleRule.Owner, Is.EqualTo(DemoTargetOwner.Friendly));
            Assert.That(cobbleRule.SlotKind, Is.EqualTo(DemoSlotKind.Building));
            Assert.That(cobbleRule.TargetType, Is.EqualTo("BUILDING"));
        }

        [Test]
        public void StructureRequiresConsecutiveBuildingSlots()
        {
            var registry = CardContentLoader.Load();
            var match = new DemoLocalMatch();
            match.ResetHand(new[] { "db_007" });
            Assert.That(registry.TryGetDefinition("db_007", out var temple), Is.True);

            Assert.That(match.TryDeploy(temple, DemoSlotKind.Building, 2, out var error), Is.False);
            Assert.That(error, Does.Contain("连续 2"));
            Assert.That(match.TryDeploy(temple, DemoSlotKind.Building, 1, out _), Is.True);
            Assert.That(match.BuildingSlots[1], Is.EqualTo("db_007"));
            Assert.That(match.BuildingSlots[2], Is.EqualTo("db_007"));
        }

        [Test]
        public void StructureDeploymentPreviewValidatesTheWholeProspectiveRange()
        {
            var registry = CardContentLoader.Load();
            var match = new DemoLocalMatch();
            match.ResetHand(new[] { "db_007" });
            Assert.That(registry.TryGetDefinition("db_007", out var temple), Is.True);

            var legal = DemoDeploymentRules.Evaluate(match, temple, DemoSlotKind.Building, 0);
            var outside = DemoDeploymentRules.Evaluate(match, temple, DemoSlotKind.Building, 2);

            Assert.That(legal.IsLegal, Is.True);
            Assert.That(legal.OccupiedSlots, Is.EqualTo(2));
            Assert.That(legal.Message, Does.Contain("1—2"));
            Assert.That(outside.IsLegal, Is.False);
            Assert.That(outside.Message, Does.Contain("连续 2"));

            match.BuildingSlots[1] = "occupied-instance";
            var overlap = DemoDeploymentRules.Evaluate(match, temple, DemoSlotKind.Building, 0);
            Assert.That(overlap.IsLegal, Is.False);
            Assert.That(overlap.Message, Does.Contain("并非全部空闲"));
        }

        [Test]
        public void CraftingPreviewAndLocalDeploymentConsumeMaterialsInsteadOfRedstone()
        {
            var registry = CardContentLoader.Load();
            var match = new DemoLocalMatch();
            match.ResetHand(new[] { "db_002", "db_007", "tk_006", "db_002" });
            Assert.That(registry.TryGetDefinition("db_007", out var temple), Is.True);

            var preview = DemoDeploymentRules.Evaluate(
                match, temple, DemoSlotKind.Building, 1, MatchPaymentMethods.Crafting);
            Assert.That(preview.IsLegal, Is.True);
            var energyBefore = match.Energy;
            var command = match.CreateDeployCommand(
                temple.id, DemoSlotKind.Building, 1, MatchPaymentMethods.Crafting);
            var result = match.ApplyDeploy(temple, command);

            Assert.That(result.Accepted, Is.True);
            Assert.That(match.Energy, Is.EqualTo(energyBefore));
            Assert.That(match.Hand, Is.EqualTo(new[] { "db_002" }));
            Assert.That(match.DiscardPile, Is.EqualTo(new[] { "db_002", "tk_006" }));
            Assert.That(match.PlayerBattlefield.Single().Health, Is.EqualTo(10));
            Assert.That(match.PlayerBattlefield.Single().MaxHealth, Is.EqualTo(10));
            Assert.That(match.BuildingSlots[1], Is.EqualTo("db_007"));
            Assert.That(match.BuildingSlots[2], Is.EqualTo("db_007"));
        }

        [Test]
        public void LocalCraftingFailureDoesNotConsumeCardsOrOccupySlots()
        {
            var registry = CardContentLoader.Load();
            var match = new DemoLocalMatch();
            match.ResetHand(new[] { "db_007", "tk_006" });
            Assert.That(registry.TryGetDefinition("db_007", out var temple), Is.True);

            var preview = DemoDeploymentRules.Evaluate(
                match, temple, DemoSlotKind.Building, 0, MatchPaymentMethods.Crafting);
            Assert.That(preview.IsLegal, Is.False);
            Assert.That(preview.Message, Does.Contain("db_002×1"));
            var result = match.ApplyDeploy(
                temple,
                match.CreateDeployCommand(temple.id, DemoSlotKind.Building, 0, MatchPaymentMethods.Crafting));

            Assert.That(result.Accepted, Is.False);
            Assert.That(result.Code, Is.EqualTo(DemoCommandRejectionCode.MissingMaterials));
            Assert.That(match.Hand, Is.EqualTo(new[] { "db_007", "tk_006" }));
            Assert.That(match.DiscardPile, Is.Empty);
            Assert.That(match.BuildingSlots.All(string.IsNullOrEmpty), Is.True);
        }

        [Test]
        public void LocalCombatReleasesEverySlotOfAThreeSlotStructure()
        {
            var registry = CardContentLoader.Load();
            var match = new DemoLocalMatch();
            Assert.That(registry.TryGetDefinition("pf_008", out var ironGolem), Is.True);
            Assert.That(registry.TryGetDefinition("ed_008", out var portalFrame), Is.True);
            match.ResetHand(new[] { ironGolem.id });
            match.ResetOpponent(new[] { portalFrame });
            Assert.That(match.OpponentBuildingSlots.All(value => value == portalFrame.id), Is.True);
            Assert.That(match.TryDeploy(ironGolem, DemoSlotKind.Unit, 0, out _), Is.True);

            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            Assert.That(match.ApplyEnterCombat(match.CreateEnterCombatCommand()).Accepted, Is.True);
            var attacker = match.GetObject(true, DemoSlotKind.Unit, 0);
            var structure = match.GetObject(false, DemoSlotKind.Building, 2);
            Assert.That(structure.OccupiedSlots, Is.EqualTo(3));
            structure.Health = 1;

            var result = match.ApplyAttack(match.CreateAttackCommand(attacker.InstanceId, "BUILDING", structure.InstanceId));

            Assert.That(result.Accepted, Is.True);
            Assert.That(attacker.Health, Is.EqualTo(7), "structures do not retaliate");
            Assert.That(match.GetObject(false, DemoSlotKind.Building, 0), Is.Null);
            Assert.That(match.OpponentBuildingSlots.All(string.IsNullOrEmpty), Is.True);
        }

        [Test]
        public void DeployCommandUsesSharedFieldsAndRevisionGuard()
        {
            var registry = CardContentLoader.Load();
            var match = new DemoLocalMatch();
            match.ResetHand(new[] { "pf_001", "pf_002" });
            Assert.That(registry.TryGetDefinition("pf_001", out var bee), Is.True);

            var command = match.CreateDeployCommand("pf_001", DemoSlotKind.Unit, 2);
            Assert.That(command.protocolVersion, Is.EqualTo(GameVersions.Protocol));
            Assert.That(command.rulesetVersion, Is.EqualTo(GameVersions.Ruleset));
            Assert.That(command.type, Is.EqualTo("DEPLOY_CARD"));
            Assert.That(command.payload.cardId, Is.EqualTo("pf_001"));
            Assert.That(command.payload.slotKind, Is.EqualTo("UNIT"));
            Assert.That(command.payload.slotIndex, Is.EqualTo(2));

            var accepted = match.ApplyDeploy(bee, command);
            Assert.That(accepted.Accepted, Is.True);
            Assert.That(accepted.Revision, Is.EqualTo(1));
            Assert.That(match.Revision, Is.EqualTo(1));

            command.expectedRevision = match.Revision;
            var duplicate = match.ApplyDeploy(bee, command);
            Assert.That(duplicate.Accepted, Is.False);
            Assert.That(duplicate.Code, Is.EqualTo(DemoCommandRejectionCode.DuplicateCommand));
            Assert.That(match.Energy, Is.EqualTo(5));

            var stale = match.CreateDeployCommand("pf_002", DemoSlotKind.Unit, 3);
            stale.expectedRevision = 0;
            Assert.That(registry.TryGetDefinition("pf_002", out var cow), Is.True);
            var rejected = match.ApplyDeploy(cow, stale);
            Assert.That(rejected.Accepted, Is.False);
            Assert.That(rejected.Code, Is.EqualTo(DemoCommandRejectionCode.RevisionMismatch));
            Assert.That(match.UnitSlots[3], Is.Null);
            Assert.That(match.Hand, Does.Contain("pf_002"));
        }

        [Test]
        public void LocalCombatResolvesRetaliationDeathAndHeroDamage()
        {
            var registry = CardContentLoader.Load();
            var match = new DemoLocalMatch();
            match.ResetHand(new[] { "pf_003" });
            Assert.That(registry.TryGetDefinition("pf_003", out var attackerDefinition), Is.True);
            Assert.That(registry.TryGetDefinition("pf_001", out var targetDefinition), Is.True);
            match.ResetOpponent(new[] { targetDefinition });

            Assert.That(match.TryDeploy(attackerDefinition, DemoSlotKind.Unit, 0, out _), Is.True);
            var attacker = match.GetObject(true, DemoSlotKind.Unit, 0);
            Assert.That(match.ApplyEnterCombat(match.CreateEnterCombatCommand()).Accepted, Is.True);
            Assert.That(match.CanAttackWith(attacker, out var summoningMessage), Is.False);
            Assert.That(summoningMessage, Does.Contain("刚被召唤"));

            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            Assert.That(match.ApplyEnterCombat(match.CreateEnterCombatCommand()).Accepted, Is.True);
            Assert.That(match.CanAttackWith(attacker, out _), Is.True);

            var target = match.GetObject(false, DemoSlotKind.Unit, 0);
            var attack = match.ApplyAttack(match.CreateAttackCommand(attacker.InstanceId, "UNIT", target.InstanceId));
            Assert.That(attack.Accepted, Is.True);
            Assert.That(attacker.Health, Is.EqualTo(1));
            Assert.That(attacker.HasAttacked, Is.True);
            Assert.That(match.GetObject(false, DemoSlotKind.Unit, 0), Is.Null);
            Assert.That(match.OpponentUnitSlots[0], Is.Empty);
        }

        [Test]
        public void LocalHuskDropsRottenFleshToItsEnemyKiller()
        {
            var registry = CardContentLoader.Load();
            var match = new DemoLocalMatch();
            Assert.That(registry.TryGetDefinition("pf_008", out var ironGolem), Is.True);
            Assert.That(registry.TryGetDefinition("db_001", out var husk), Is.True);
            Assert.That(husk.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));
            match.ResetDeckAndHand(new[] { ironGolem.id }, new string[0]);
            match.ResetOpponent(new[] { husk });
            Assert.That(match.TryDeploy(ironGolem, DemoSlotKind.Unit, 0, out _), Is.True);
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            Assert.That(match.ApplyEnterCombat(match.CreateEnterCombatCommand()).Accepted, Is.True);

            var attacker = match.GetObject(true, DemoSlotKind.Unit, 0);
            var target = match.GetObject(false, DemoSlotKind.Unit, 0);
            var result = match.ApplyAttack(match.CreateAttackCommand(attacker.InstanceId, "UNIT", target.InstanceId));

            Assert.That(result.Accepted, Is.True);
            Assert.That(result.Message, Does.Contain("尸壳掉落"));
            Assert.That(match.Hand, Is.EqualTo(new[] { "tk_005" }));
            Assert.That(match.GetObject(false, DemoSlotKind.Unit, 0), Is.Null);
        }

        [Test]
        public void LocalRetaliationCreditsHuskLootToTheOpponent()
        {
            var registry = CardContentLoader.Load();
            var match = new DemoLocalMatch();
            Assert.That(registry.TryGetDefinition("db_001", out var husk), Is.True);
            Assert.That(registry.TryGetDefinition("nt_003", out var blaze), Is.True);
            match.ResetDeckAndHand(new[] { husk.id }, new string[0]);
            match.ResetOpponent(new[] { blaze });
            Assert.That(match.TryDeploy(husk, DemoSlotKind.Unit, 0, out _), Is.True);
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            Assert.That(match.ApplyEnterCombat(match.CreateEnterCombatCommand()).Accepted, Is.True);

            var attacker = match.GetObject(true, DemoSlotKind.Unit, 0);
            var target = match.GetObject(false, DemoSlotKind.Unit, 0);
            var result = match.ApplyAttack(match.CreateAttackCommand(attacker.InstanceId, "UNIT", target.InstanceId));

            Assert.That(result.Accepted, Is.True);
            Assert.That(result.Message, Does.Contain("对手获得一张腐肉"));
            Assert.That(match.OpponentHandCount, Is.EqualTo(6));
            Assert.That(match.DiscardPile, Is.EqualTo(new[] { "db_001" }));
        }

        [Test]
        public void LocalSandstormDropsOnlyTheEnemyHuskLoot()
        {
            var registry = CardContentLoader.Load();
            var match = new DemoLocalMatch();
            Assert.That(registry.TryGetDefinition("db_001", out var husk), Is.True);
            Assert.That(registry.TryGetDefinition("db_006", out var sandstorm), Is.True);
            match.ResetHand(new[] { husk.id, sandstorm.id });
            match.ResetOpponent(new[] { husk });
            Assert.That(match.TryDeploy(husk, DemoSlotKind.Unit, 0, out _), Is.True);

            var result = match.ApplyPlayCard(sandstorm, match.CreatePlayCardCommand(sandstorm.id));

            Assert.That(result.Accepted, Is.True);
            Assert.That(result.Message, Does.Contain("尸壳掉落"));
            Assert.That(match.Hand, Is.EqualTo(new[] { "tk_005" }));
            Assert.That(match.DiscardPile, Is.EqualTo(new[] { "db_006", "db_001" }));
            Assert.That(match.OpponentHandCount, Is.EqualTo(5));
        }

        [Test]
        public void LocalDungeonSkeletonDeathrattleDamagesBeforeDroppingBone()
        {
            var registry = CardContentLoader.Load();
            var match = new DemoLocalMatch();
            Assert.That(registry.TryGetDefinition("pf_008", out var ironGolem), Is.True);
            Assert.That(registry.TryGetDefinition("cd_003", out var dungeonSkeleton), Is.True);
            Assert.That(dungeonSkeleton.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));
            match.ResetDeckAndHand(new[] { ironGolem.id }, new string[0]);
            match.ResetOpponent(new[] { dungeonSkeleton });
            Assert.That(match.TryDeploy(ironGolem, DemoSlotKind.Unit, 0, out _), Is.True);
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            Assert.That(match.ApplyEnterCombat(match.CreateEnterCombatCommand()).Accepted, Is.True);

            var attacker = match.GetObject(true, DemoSlotKind.Unit, 0);
            var target = match.GetObject(false, DemoSlotKind.Unit, 0);
            var result = match.ApplyAttack(match.CreateAttackCommand(attacker.InstanceId, "UNIT", target.InstanceId));

            Assert.That(result.Accepted, Is.True);
            Assert.That(attacker.Health, Is.EqualTo(3));
            Assert.That(match.Hand, Is.EqualTo(new[] { "tk_009" }));
            var deathrattleIndex = result.Message.IndexOf("地牢骷髅亡语", System.StringComparison.Ordinal);
            var dropIndex = result.Message.IndexOf("地牢骷髅掉落", System.StringComparison.Ordinal);
            Assert.That(deathrattleIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(dropIndex, Is.GreaterThan(deathrattleIndex));
        }

        [Test]
        public void LocalDungeonSkeletonSkipsDeathrattleWithoutALegalEnemyUnit()
        {
            var registry = CardContentLoader.Load();
            var match = new DemoLocalMatch();
            Assert.That(registry.TryGetDefinition("pf_003", out var wolf), Is.True);
            Assert.That(registry.TryGetDefinition("cd_003", out var dungeonSkeleton), Is.True);
            match.ResetDeckAndHand(new[] { wolf.id }, new string[0]);
            match.ResetOpponent(new[] { dungeonSkeleton });
            Assert.That(match.TryDeploy(wolf, DemoSlotKind.Unit, 0, out _), Is.True);
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            Assert.That(match.ApplyEnterCombat(match.CreateEnterCombatCommand()).Accepted, Is.True);

            var attacker = match.GetObject(true, DemoSlotKind.Unit, 0);
            var target = match.GetObject(false, DemoSlotKind.Unit, 0);
            var result = match.ApplyAttack(match.CreateAttackCommand(attacker.InstanceId, "UNIT", target.InstanceId));

            Assert.That(result.Accepted, Is.True);
            Assert.That(match.PlayerBattlefield, Is.Empty);
            Assert.That(match.OpponentBattlefield, Is.Empty);
            Assert.That(match.Hand, Is.EqualTo(new[] { "tk_009" }));
            Assert.That(result.Message, Does.Not.Contain("地牢骷髅亡语"));
            Assert.That(result.Message, Does.Contain("地牢骷髅掉落"));
        }

        [Test]
        public void LocalDungeonSkeletonPropagatesAChainedHuskKillCredit()
        {
            var registry = CardContentLoader.Load();
            var match = new DemoLocalMatch();
            Assert.That(registry.TryGetDefinition("pf_003", out var wolf), Is.True);
            Assert.That(registry.TryGetDefinition("db_001", out var husk), Is.True);
            Assert.That(registry.TryGetDefinition("cd_003", out var dungeonSkeleton), Is.True);
            match.ResetDeckAndHand(new[] { wolf.id, husk.id }, new string[0]);
            match.ResetOpponent(new[] { dungeonSkeleton });
            Assert.That(match.TryDeploy(wolf, DemoSlotKind.Unit, 0, out _), Is.True);
            Assert.That(match.TryDeploy(husk, DemoSlotKind.Unit, 1, out _), Is.True);
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            Assert.That(match.ApplyEnterCombat(match.CreateEnterCombatCommand()).Accepted, Is.True);

            var attacker = match.GetObject(true, DemoSlotKind.Unit, 0);
            var target = match.GetObject(false, DemoSlotKind.Unit, 0);
            var result = match.ApplyAttack(match.CreateAttackCommand(attacker.InstanceId, "UNIT", target.InstanceId));

            Assert.That(result.Accepted, Is.True);
            Assert.That(match.PlayerBattlefield, Is.Empty);
            Assert.That(match.OpponentBattlefield, Is.Empty);
            Assert.That(match.Hand, Is.EqualTo(new[] { "tk_009" }));
            Assert.That(match.OpponentHandCount, Is.EqualTo(6));
            Assert.That(result.Message, Does.Contain("对手获得一张腐肉"));
        }

        [Test]
        public void LocalGrazingSheepDropsWoolToItsEnemyKiller()
        {
            var registry = CardContentLoader.Load();
            var match = new DemoLocalMatch();
            Assert.That(registry.TryGetDefinition("pf_008", out var ironGolem), Is.True);
            Assert.That(registry.TryGetDefinition("pf_002", out var sheep), Is.True);
            Assert.That(sheep.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));
            match.ResetDeckAndHand(new[] { ironGolem.id }, new string[0]);
            match.ResetOpponent(new[] { sheep });
            Assert.That(match.TryDeploy(ironGolem, DemoSlotKind.Unit, 0, out _), Is.True);
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            Assert.That(match.ApplyEnterCombat(match.CreateEnterCombatCommand()).Accepted, Is.True);

            var attacker = match.GetObject(true, DemoSlotKind.Unit, 0);
            var target = match.GetObject(false, DemoSlotKind.Unit, 0);
            var result = match.ApplyAttack(match.CreateAttackCommand(attacker.InstanceId, "UNIT", target.InstanceId));

            Assert.That(result.Accepted, Is.True);
            Assert.That(match.Hand, Is.EqualTo(new[] { "tk_001" }));
            Assert.That(result.Message, Does.Contain("放牧绵羊掉落"));
        }

        [Test]
        public void LocalCombatAllowsChargeOnTheSummonedRound()
        {
            var registry = CardContentLoader.Load();
            var match = new DemoLocalMatch();
            match.ResetHand(new[] { "pf_001" });
            Assert.That(registry.TryGetDefinition("pf_001", out var definition), Is.True);
            Assert.That(match.TryDeploy(definition, DemoSlotKind.Unit, 0, out _), Is.True);
            var attacker = match.GetObject(true, DemoSlotKind.Unit, 0);
            attacker.Keywords = new[] { "CHARGE" };
            Assert.That(match.ApplyEnterCombat(match.CreateEnterCombatCommand()).Accepted, Is.True);
            Assert.That(match.CanAttackWith(attacker, out _), Is.True);

            var result = match.ApplyAttack(match.CreateAttackCommand(attacker.InstanceId, "HERO"));

            Assert.That(result.Accepted, Is.True);
            Assert.That(match.OpponentLife, Is.EqualTo(29));
        }

        [Test]
        public void LocalCombatHighlightsAndEnforcesTauntTargets()
        {
            var registry = CardContentLoader.Load();
            var match = new DemoLocalMatch();
            match.ResetHand(new[] { "pf_003" });
            Assert.That(registry.TryGetDefinition("pf_003", out var attackerDefinition), Is.True);
            Assert.That(registry.TryGetDefinition("pf_008", out var tauntDefinition), Is.True);
            Assert.That(registry.TryGetDefinition("pf_001", out var normalDefinition), Is.True);
            match.ResetOpponent(new[] { tauntDefinition, normalDefinition });
            Assert.That(match.TryDeploy(attackerDefinition, DemoSlotKind.Unit, 0, out _), Is.True);
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            Assert.That(match.ApplyEnterCombat(match.CreateEnterCombatCommand()).Accepted, Is.True);

            var attacker = match.GetObject(true, DemoSlotKind.Unit, 0);
            var taunt = match.GetObject(false, DemoSlotKind.Unit, 0);
            var normal = match.GetObject(false, DemoSlotKind.Unit, 2);
            Assert.That(taunt.HasKeyword("TAUNT"), Is.True);
            Assert.That(match.CanAttackTarget(null, "HERO", out var heroMessage), Is.False);
            Assert.That(heroMessage, Does.Contain("嘲讽"));
            Assert.That(match.CanAttackTarget(normal, "UNIT", out _), Is.False);
            Assert.That(match.CanAttackTarget(taunt, "UNIT", out _), Is.True);

            var bypass = match.ApplyAttack(match.CreateAttackCommand(attacker.InstanceId, "HERO"));
            Assert.That(bypass.Accepted, Is.False);
            Assert.That(bypass.Code, Is.EqualTo(DemoCommandRejectionCode.TauntTargetRequired));
            Assert.That(attacker.HasAttacked, Is.False);

            var legal = match.ApplyAttack(match.CreateAttackCommand(attacker.InstanceId, "UNIT", taunt.InstanceId));
            Assert.That(legal.Accepted, Is.True);
        }

        [Test]
        public void LocalShulkerDeathrattleGeneratesShulkerShell()
        {
            var registry = CardContentLoader.Load();
            var match = new DemoLocalMatch();
            Assert.That(registry.TryGetDefinition("ed_004", out var shulker), Is.True);
            Assert.That(registry.TryGetDefinition("pf_008", out var ironGolem), Is.True);
            match.ResetHand(new[] { shulker.id });
            match.ResetOpponent(new[] { ironGolem });
            Assert.That(match.TryDeploy(shulker, DemoSlotKind.Unit, 0, out _), Is.True);
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            Assert.That(match.ApplyEnterCombat(match.CreateEnterCombatCommand()).Accepted, Is.True);

            var attacker = match.GetObject(true, DemoSlotKind.Unit, 0);
            var target = match.GetObject(false, DemoSlotKind.Unit, 0);
            var result = match.ApplyAttack(match.CreateAttackCommand(attacker.InstanceId, "UNIT", target.InstanceId));

            Assert.That(result.Accepted, Is.True);
            Assert.That(result.Message, Does.Contain("潜影贝亡语"));
            Assert.That(match.Hand, Is.EqualTo(new[] { "tk_016" }));
            Assert.That(match.DiscardPile, Is.EqualTo(new[] { "ed_004" }));
        }

        [Test]
        public void LocalShulkerDeathrattleDiscardsShellAtFullHand()
        {
            var registry = CardContentLoader.Load();
            var match = new DemoLocalMatch();
            Assert.That(registry.TryGetDefinition("ed_004", out var shulker), Is.True);
            Assert.That(registry.TryGetDefinition("pf_008", out var ironGolem), Is.True);
            match.ResetDeckAndHand(
                new[] { "ed_004", "ed_001", "ed_001", "ed_001", "ed_001", "ed_001", "ed_001" },
                new[] { "ed_003" });
            match.ResetOpponent(new[] { ironGolem });
            Assert.That(match.TryDeploy(shulker, DemoSlotKind.Unit, 0, out _), Is.True);
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            Assert.That(match.Hand.Count, Is.EqualTo(7));
            Assert.That(match.ApplyEnterCombat(match.CreateEnterCombatCommand()).Accepted, Is.True);

            var attacker = match.GetObject(true, DemoSlotKind.Unit, 0);
            var target = match.GetObject(false, DemoSlotKind.Unit, 0);
            var result = match.ApplyAttack(match.CreateAttackCommand(attacker.InstanceId, "UNIT", target.InstanceId));

            Assert.That(result.Accepted, Is.True);
            Assert.That(result.Message, Does.Contain("手牌已满"));
            Assert.That(match.Hand.Count, Is.EqualTo(7));
            Assert.That(match.DiscardPile, Is.EqualTo(new[] { "ed_004", "tk_016" }));
        }

        [Test]
        public void LocalMagmaCubeDeathrattleSummonsTokenIntoReleasedSlot()
        {
            var registry = CardContentLoader.Load();
            var match = new DemoLocalMatch();
            Assert.That(registry.TryGetDefinition("nt_001", out var magmaCube), Is.True);
            Assert.That(registry.TryGetDefinition("pf_008", out var ironGolem), Is.True);
            match.ResetHand(new[] { magmaCube.id });
            match.ResetOpponent(new[] { ironGolem });
            Assert.That(match.TryDeploy(magmaCube, DemoSlotKind.Unit, 2, out _), Is.True);
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            Assert.That(match.ApplyEnterCombat(match.CreateEnterCombatCommand()).Accepted, Is.True);

            var attacker = match.GetObject(true, DemoSlotKind.Unit, 2);
            var target = match.GetObject(false, DemoSlotKind.Unit, 0);
            var result = match.ApplyAttack(match.CreateAttackCommand(attacker.InstanceId, "UNIT", target.InstanceId));

            Assert.That(result.Accepted, Is.True);
            Assert.That(result.Message, Does.Contain("岩浆怪亡语"));
            Assert.That(result.Message, Does.Contain("单位格 3"));
            Assert.That(match.UnitSlots[2], Is.EqualTo("tk_014"));
            var token = match.GetObject(true, DemoSlotKind.Unit, 2);
            Assert.That(token.CardId, Is.EqualTo("tk_014"));
            Assert.That(token.Attack, Is.EqualTo(1));
            Assert.That(token.Health, Is.EqualTo(1));
            Assert.That(token.SummonedRound, Is.EqualTo(match.Round));
            Assert.That(match.CanAttackWith(token, out var message), Is.False);
            Assert.That(message, Does.Contain("冲锋"));
        }

        [Test]
        public void LocalSimultaneousMagmaDeathsResolveBothSummonsAfterTheDeathBatch()
        {
            var registry = CardContentLoader.Load();
            var match = new DemoLocalMatch();
            Assert.That(registry.TryGetDefinition("nt_001", out var magmaCube), Is.True);
            Assert.That(registry.TryGetDefinition("db_006", out var sandstorm), Is.True);
            match.ResetHand(new[] { magmaCube.id, sandstorm.id });
            match.ResetOpponent(new[] { magmaCube });
            Assert.That(match.TryDeploy(magmaCube, DemoSlotKind.Unit, 1, out _), Is.True);

            var result = match.ApplyPlayCard(sandstorm, match.CreatePlayCardCommand(sandstorm.id));

            Assert.That(result.Accepted, Is.True);
            Assert.That(match.UnitSlots[1], Is.EqualTo("tk_014"));
            Assert.That(match.OpponentUnitSlots[0], Is.EqualTo("tk_014"));
            Assert.That(match.GetObject(true, DemoSlotKind.Unit, 1).CardId, Is.EqualTo("tk_014"));
            Assert.That(match.GetObject(false, DemoSlotKind.Unit, 0).CardId, Is.EqualTo("tk_014"));
            var ownMessageIndex = result.Message.IndexOf("岩浆怪亡语", System.StringComparison.Ordinal);
            var enemyMessageIndex = result.Message.IndexOf("敌方岩浆怪亡语", System.StringComparison.Ordinal);
            Assert.That(ownMessageIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(enemyMessageIndex, Is.GreaterThan(ownMessageIndex));
        }

        [Test]
        public void LocalCombatCanDefeatOpponentHero()
        {
            var registry = CardContentLoader.Load();
            var match = new DemoLocalMatch();
            match.ResetHand(new[] { "pf_003" });
            Assert.That(registry.TryGetDefinition("pf_003", out var attackerDefinition), Is.True);
            Assert.That(match.TryDeploy(attackerDefinition, DemoSlotKind.Unit, 0, out _), Is.True);
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            Assert.That(match.ApplyEnterCombat(match.CreateEnterCombatCommand()).Accepted, Is.True);
            var attacker = match.GetObject(true, DemoSlotKind.Unit, 0);

            DemoCommandResult result = null;
            while (!match.IsFinished)
            {
                attacker.HasAttacked = false;
                result = match.ApplyAttack(match.CreateAttackCommand(attacker.InstanceId, "HERO"));
                Assert.That(result.Accepted, Is.True);
            }

            Assert.That(match.OpponentLife, Is.Zero);
            Assert.That(result.Message, Does.Contain("胜利"));
        }

        [Test]
        public void GeneratedSceneAndRuntimeHierarchyExist()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(DemoSceneBuilder.ScenePath), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(DemoUiPrefabBuilder.PrefabFolder + "/BasePanel.prefab").GetComponent<BasePanel>(), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(DemoUiPrefabBuilder.PrefabFolder + "/SecondaryButton.prefab").GetComponent<SecondaryButton>(), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(DemoUiPrefabBuilder.PrefabFolder + "/PrimaryActionButton.prefab").GetComponent<PrimaryActionButton>(), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(DemoUiPrefabBuilder.PrefabFolder + "/CardUI.prefab").GetComponent<CardUI>(), Is.Not.Null);

            var registry = CardContentLoader.Load();
            var root = new GameObject("DemoTestRoot");
            try
            {
                var configuredBattlefield = root.AddComponent<DemoBattlefield3D>();
                configuredBattlefield.Configure(
                    Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"),
                    Shader.Find("BiomeRivals/Demo/CompositeBackdrop"),
                    Shader.Find("BiomeRivals/Demo/GroundSurface"),
                    AssetDatabase.LoadAssetAtPath<Texture2D>(DemoSceneBuilder.BackgroundPath));
                var controller = root.AddComponent<DemoSceneController>();
                controller.BuildNow();
                var battlefield = root.GetComponent<DemoBattlefield3D>();
                Assert.That(battlefield, Is.Not.Null);
                Assert.That(battlefield.BoardCamera, Is.Not.Null);
                Assert.That(battlefield.BoardCamera.orthographic, Is.False);
                Assert.That(battlefield.BoardCamera.fieldOfView, Is.EqualTo(42.5f).Within(0.01f));
                Assert.That(root.transform.Find("BattlefieldGeometry"), Is.Not.Null);
                Assert.That(root.transform.Find("BattlefieldPieces"), Is.Not.Null);
                var unitMarker = root.transform.Find("BattlefieldGeometry/SlotMarker_Player_Unit_0/InteractiveGround");
                var opponentUnitMarker = root.transform.Find("BattlefieldGeometry/SlotMarker_Opponent_Unit_0/InteractiveGround");
                var buildingMarker = root.transform.Find("BattlefieldGeometry/SlotMarker_Player_Building_0/InteractiveGround");
                var unitRiser = root.transform.Find("BattlefieldGeometry/SlotMarker_Player_Unit_0/GroundRiser");
                Assert.That(unitMarker, Is.Not.Null);
                Assert.That(buildingMarker, Is.Not.Null);
                Assert.That(unitRiser, Is.Not.Null);
                Assert.That(unitMarker.IsChildOf(root.transform.Find("DemoCanvas")), Is.False);
                Assert.That(root.GetComponent<DemoBattlefieldPointerController>(), Is.Not.Null);
                Assert.That(unitMarker.GetComponent<MeshCollider>(), Is.Not.Null);
                var unitTarget = unitMarker.GetComponent<DemoBattlefieldSlotTarget>();
                Assert.That(unitTarget, Is.Not.Null);
                Assert.That(unitTarget.Player, Is.True);
                Assert.That(unitTarget.Kind, Is.EqualTo(DemoSlotKind.Unit));
                Assert.That(unitTarget.Index, Is.Zero);
                Assert.That(unitMarker.GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.EqualTo(60));
                Assert.That(unitMarker.GetComponent<MeshRenderer>().enabled, Is.True);
                Assert.That(buildingMarker.GetComponent<MeshRenderer>().enabled, Is.True);
                Assert.That(unitMarker.GetComponent<MeshRenderer>().sharedMaterial.shader.name, Is.EqualTo("BiomeRivals/Demo/GroundSurface"));
                Assert.That(unitMarker.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_UseScreenProjection"), Is.EqualTo(1f));
                Assert.That(unitMarker.GetComponent<MeshRenderer>().sharedMaterial.mainTexture.name, Is.EqualTo("field-plains_forest-v1"));
                Assert.That(opponentUnitMarker.GetComponent<MeshRenderer>().sharedMaterial.mainTexture.name, Is.EqualTo("field-nether-far-v1"));
                Assert.That(unitMarker.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_HighlightStrength"), Is.GreaterThan(0f));
                Assert.That(buildingMarker.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_HighlightStrength"), Is.Zero);
                Physics.SyncTransforms();
                var unitScreenPosition = battlefield.BoardCamera.WorldToScreenPoint(unitMarker.TransformPoint(unitMarker.GetComponent<MeshFilter>().sharedMesh.bounds.center));
                Assert.That(battlefield.TryRaycastSlot(unitScreenPosition, out var raycastTarget), Is.True);
                Assert.That(raycastTarget, Is.SameAs(unitTarget));
                var opponentScreenPosition = battlefield.BoardCamera.WorldToScreenPoint(opponentUnitMarker.TransformPoint(opponentUnitMarker.GetComponent<MeshFilter>().sharedMesh.bounds.center));
                Assert.That(battlefield.TryRaycastSlot(opponentScreenPosition, out var opponentRaycastTarget), Is.True);
                Assert.That(opponentRaycastTarget.Player, Is.False);
                var unitMeshVertices = unitMarker.GetComponent<MeshFilter>().sharedMesh.vertices;
                var nearZ = unitMeshVertices.Min(vertex => vertex.z);
                var farZ = unitMeshVertices.Max(vertex => vertex.z);
                var nearVertices = unitMeshVertices.Where(vertex => Mathf.Abs(vertex.z - nearZ) < 0.001f).ToArray();
                var farVertices = unitMeshVertices.Where(vertex => Mathf.Abs(vertex.z - farZ) < 0.001f).ToArray();
                var nearWidth = ProjectedWidth(battlefield.BoardCamera, unitMarker, nearVertices);
                var farWidth = ProjectedWidth(battlefield.BoardCamera, unitMarker, farVertices);
                Assert.That(nearWidth, Is.GreaterThan(farWidth));
                Assert.That(root.transform.Find("DemoCanvas"), Is.Not.Null);
                var canvas = root.transform.Find("DemoCanvas").GetComponent<UnityEngine.Canvas>();
                var scaler = root.transform.Find("DemoCanvas").GetComponent<UnityEngine.UI.CanvasScaler>();
                Assert.That(canvas.pixelPerfect, Is.True);
                Assert.That(scaler.referencePixelsPerUnit, Is.EqualTo(DemoUiMetrics.PixelsPerUnit));
                Assert.That(GameObject.Find("EndTurnButton"), Is.Not.Null);
                Assert.That(GameObject.Find("Faction_plains_forest"), Is.Not.Null);
                Assert.That(root.transform.Find("DemoCanvas/OnlineStatusPanel/Status").GetComponent<UnityEngine.UI.Text>().text, Is.EqualTo("本地模式"));
                Assert.That(root.transform.Find("DemoCanvas/OnlineStatusPanel/OnlineAction").GetComponent<SecondaryButton>(), Is.Not.Null);
                var playerSlotHitArea = root.transform.Find("DemoCanvas/PlayerUnitSlot0");
                Assert.That(playerSlotHitArea.GetComponent<UnityEngine.UI.Graphic>(), Is.Null);
                Assert.That(playerSlotHitArea.GetComponent<UnityEngine.UI.Button>(), Is.Null);
                Assert.That(root.transform.Find("DemoCanvas/OpponentUnitSlot0").GetComponent<UnityEngine.UI.Graphic>(), Is.Null);
                Assert.That(root.transform.Find("DemoCanvas/OpponentHUD").GetComponent<UnityEngine.UI.Button>(), Is.Not.Null);
                Assert.That(root.transform.Find("DemoCanvas/OpponentHUD/Health").GetComponent<UnityEngine.UI.Text>().text, Does.Contain("30"));
                var opponentHud = root.transform.Find("DemoCanvas/OpponentHUD");
                Assert.That(opponentHud, Is.Not.Null);
                Assert.That(opponentHud.GetComponent<BasePanel>(), Is.Not.Null);
                Assert.That(opponentHud.GetComponent<UnityEngine.UI.Outline>(), Is.Null);
                var materialFill = opponentHud.Find("MaterialFill")?.GetComponent<UnityEngine.UI.Image>();
                var frameSlice = opponentHud.Find("FrameSlice")?.GetComponent<UnityEngine.UI.Image>();
                Assert.That(materialFill, Is.Not.Null);
                Assert.That(materialFill.type, Is.EqualTo(UnityEngine.UI.Image.Type.Tiled));
                Assert.That(materialFill.sprite.pixelsPerUnit, Is.EqualTo(DemoUiMetrics.PixelsPerUnit));
                Assert.That(frameSlice, Is.Not.Null);
                Assert.That(frameSlice.type, Is.EqualTo(UnityEngine.UI.Image.Type.Sliced));
                Assert.That(frameSlice.pixelsPerUnitMultiplier, Is.EqualTo(1f));
                Assert.That(frameSlice.sprite.pixelsPerUnit, Is.EqualTo(DemoUiMetrics.PixelsPerUnit));
                Assert.That(frameSlice.sprite.border, Is.EqualTo(Vector4.one * DemoUiMetrics.FrameBorderPixels));
                Assert.That(opponentHud.Find("FrameTop"), Is.Null);
                Assert.That(opponentHud.Find("FrameCornerNW"), Is.Null);
                Assert.That(opponentHud.Find("RivetNW")?.GetComponent<UnityEngine.UI.Image>(), Is.Not.Null);
                Assert.That(frameSlice.sprite.texture.name, Does.Contain("stone_bricks"));
                Assert.That(root.transform.Find("DemoCanvas/PlayerHUD/FrameSlice").GetComponent<UnityEngine.UI.Image>().sprite.texture.name, Does.Contain("stone_bricks"));
                Assert.That(root.transform.Find("DemoCanvas/PlayerHUD/EffectFlash").GetComponent<CanvasGroup>().alpha, Is.Zero);
                Assert.That(root.transform.Find("DemoCanvas/CardDetailsPanel/FrameSlice").GetComponent<UnityEngine.UI.Image>().sprite.texture.name, Does.Contain("stone_bricks"));
                Assert.That(root.transform.Find("DemoCanvas/HandPlate/FrameSlice").GetComponent<UnityEngine.UI.Image>().sprite.texture.name, Does.Contain("stone_bricks"));
                Assert.That(root.transform.Find("DemoCanvas/HandLabel").GetComponent<UnityEngine.UI.Text>().text, Does.Contain("牌库 25"));
                Assert.That(root.transform.Find("DemoCanvas/EndTurnButton/FrameSlice").GetComponent<UnityEngine.UI.Image>().sprite.texture.name, Does.Contain("prismarine_bricks"));
                var endTurn = root.transform.Find("DemoCanvas/EndTurnButton");
                Assert.That(endTurn.GetComponent<PrimaryActionButton>(), Is.Not.Null);
                Assert.That(endTurn.GetComponent<SecondaryButton>(), Is.Null);
                Assert.That(root.GetComponentsInChildren<PrimaryActionButton>(true), Has.Length.EqualTo(3));
                var mulliganOverlay = root.transform.Find("DemoCanvas/MulliganOverlay");
                Assert.That(mulliganOverlay, Is.Not.Null);
                Assert.That(mulliganOverlay.gameObject.activeSelf, Is.False, "Opening-hand UI stays hidden in the offline sandbox.");
                Assert.That(mulliganOverlay.Find("MulliganPanel/ConfirmMulligan").GetComponent<PrimaryActionButton>(), Is.Not.Null);
                var choiceOverlay = root.transform.Find("DemoCanvas/ChoiceOverlay");
                Assert.That(choiceOverlay, Is.Not.Null);
                Assert.That(choiceOverlay.gameObject.activeSelf, Is.False, "Card choices stay hidden until an effect offers one.");
                Assert.That(choiceOverlay.Find("ArchaeologyPanel/ConfirmChoice").GetComponent<PrimaryActionButton>(), Is.Not.Null);
                var factionButtons = root.GetComponentsInChildren<SecondaryButton>(true)
                    .Where(style => style.name.StartsWith("Faction_", System.StringComparison.Ordinal))
                    .ToArray();
                Assert.That(factionButtons, Has.Length.EqualTo(7));
                var neutralButtonColor = DemoUiStyleCatalog.GetRootFill(DemoUiStyleClass.SecondaryButton);
                foreach (var style in factionButtons)
                {
                    Assert.That(style.GetComponent<UnityEngine.UI.Image>().color, Is.EqualTo(neutralButtonColor));
                    Assert.That(style.transform.Find("FrameSlice").GetComponent<UnityEngine.UI.Image>().sprite.texture.name, Does.Contain("stone_bricks"));
                }
                Assert.That(root.transform.Find("DemoCanvas/FactionRail/Faction_plains_forest/SelectionAccent").gameObject.activeSelf, Is.True);
                Assert.That(root.transform.Find("DemoCanvas/FactionRail/Faction_desert_badlands/SelectionAccent").gameObject.activeSelf, Is.False);
                Assert.That(root.transform.Find("DemoCanvas/CardDetailsPanel/InspectorContent").GetComponent<RectTransform>().sizeDelta, Is.EqualTo(new Vector2(286f, 701f)));
                var themedCard = GameObject.Find("Card_pf_001");
                Assert.That(themedCard, Is.Not.Null);
                var handCard = root.transform.Find("DemoCanvas/HandPlate/HandCards/Card_pf_001").GetComponent<CardUI>();
                var detailCard = root.transform.Find("DemoCanvas/CardDetailsPanel/InspectorContent/Card_pf_001").GetComponent<CardUI>();
                var detailsView = root.transform.Find("DemoCanvas/CardDetailsPanel/InspectorContent").GetComponent<CardDetailsView>();
                Assert.That(handCard, Is.Not.Null);
                Assert.That(detailCard, Is.Not.Null);
                Assert.That(handCard.CardId, Is.EqualTo(detailCard.CardId));
                Assert.That(handCard.IsCompact, Is.True);
                Assert.That(detailCard.IsCompact, Is.False);
                Assert.That(detailsView.CurrentCard, Is.SameAs(detailCard));
                Assert.That(themedCard.GetComponent<UnityEngine.UI.Image>().sprite.name, Is.EqualTo("CardFrame_plains_forest"));
                Assert.That(themedCard.transform.Find("MaterialFill"), Is.Null);
                Assert.That(themedCard.transform.Find("TitleBand"), Is.Null);
                Assert.That(themedCard.transform.Find("CostSocket"), Is.Null);
                Assert.That(themedCard.transform.Find("CostSocketFrame").GetComponent<UnityEngine.UI.Image>().sprite.name, Is.EqualTo("CardCostSocket_plains_forest"));
                Assert.That(themedCard.transform.Find("AttackSocketFrame").GetComponent<UnityEngine.UI.Image>().sprite.name, Is.EqualTo("CardAttackSocket_plains_forest"));
                Assert.That(themedCard.transform.Find("HealthSocketFrame").GetComponent<UnityEngine.UI.Image>().sprite.name, Is.EqualTo("CardHealthSocket_plains_forest"));
                var artSurface = themedCard.transform.Find("ArtSurface").GetComponent<UnityEngine.UI.Image>();
                Assert.That(artSurface.type, Is.EqualTo(UnityEngine.UI.Image.Type.Tiled));
                Assert.That(artSurface.sprite.name, Is.EqualTo("CardArtSurface_polished_blackstone_bricks"));
                var themedRules = themedCard.transform.Find("Rules").GetComponent<UnityEngine.UI.Text>();
                Assert.That(themedRules.alignment, Is.EqualTo(UnityEngine.TextAnchor.MiddleCenter));
                Assert.That(themedRules.alignByGeometry, Is.True);
                Assert.That(themedRules.rectTransform.anchoredPosition.x, Is.Zero.Within(0.001f));
                Assert.That(themedRules.rectTransform.anchoredPosition.y, Is.EqualTo(-themedCard.GetComponent<RectTransform>().sizeDelta.y * 0.21f).Within(0.01f));
                var frameMappings = new[,]
                {
                    { "plains_forest", "pf" },
                    { "desert_badlands", "db" },
                    { "snow_ice", "si" },
                    { "cave_dark_forest", "cd" },
                    { "ocean_river", "or" },
                    { "nether", "nt" },
                    { "end", "ed" }
                };
                for (var mapping = 0; mapping < frameMappings.GetLength(0); mapping++)
                {
                    var themeId = frameMappings[mapping, 0];
                    var prefix = frameMappings[mapping, 1];
                    GameObject.Find("Faction_" + themeId).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                    Assert.That(battlefield.PlayerFactionId, Is.EqualTo(themeId));
                    Assert.That(unitMarker.GetComponent<MeshRenderer>().sharedMaterial.mainTexture.name, Is.EqualTo("field-" + themeId + "-v1"));
                    var mappedCardId = prefix + "_001";
                    var card = GameObject.Find("Card_" + mappedCardId);
                    Assert.That(card, Is.Not.Null, themeId);
                    Assert.That(card.GetComponent<UnityEngine.UI.Image>().sprite.name, Is.EqualTo("CardFrame_" + themeId));
                    Assert.That(card.transform.Find("CostSocketFrame").GetComponent<UnityEngine.UI.Image>().sprite.name, Is.EqualTo("CardCostSocket_" + themeId));
                    Assert.That(registry.TryGetDefinition(mappedCardId, out var mappedDefinition), Is.True);
                    if (mappedDefinition.hasAttack)
                        Assert.That(card.transform.Find("AttackSocketFrame").GetComponent<UnityEngine.UI.Image>().sprite.name, Is.EqualTo("CardAttackSocket_" + themeId));
                    else
                        Assert.That(card.transform.Find("AttackSocketFrame"), Is.Null);
                    if (mappedDefinition.hasHealth)
                        Assert.That(card.transform.Find("HealthSocketFrame").GetComponent<UnityEngine.UI.Image>().sprite.name, Is.EqualTo("CardHealthSocket_" + themeId));
                    else
                        Assert.That(card.transform.Find("HealthSocketFrame"), Is.Null);
                    if (mappedDefinition.hasDurability)
                        Assert.That(card.transform.Find("DurabilitySocketFrame").GetComponent<UnityEngine.UI.Image>().sprite.name, Is.EqualTo("CardHealthSocket_" + themeId));
                }

                var backdropRenderer = root.transform.Find("BattlefieldCamera/IllustratedBattlefieldBackdrop").GetComponent<MeshRenderer>();
                Assert.That(backdropRenderer.sharedMaterial.shader.name, Is.EqualTo("BiomeRivals/Demo/CompositeBackdrop"));
                Assert.That(backdropRenderer.sharedMaterial.GetTexture("_PlayerTex").name, Is.EqualTo("field-end-v1"));
                Assert.That(backdropRenderer.sharedMaterial.GetTexture("_OpponentTex").name, Is.EqualTo("field-nether-far-v1"));
                var nextOpponent = root.transform.Find("DemoCanvas/OpponentFactionSelector/NextOpponentFaction").GetComponent<UnityEngine.UI.Button>();
                nextOpponent.onClick.Invoke();
                Assert.That(battlefield.OpponentFactionId, Is.EqualTo("end"));
                Assert.That(opponentUnitMarker.GetComponent<MeshRenderer>().sharedMaterial.mainTexture.name, Is.EqualTo("field-end-far-v1"));
                Assert.That(backdropRenderer.sharedMaterial.GetTexture("_OpponentTex").name, Is.EqualTo("field-end-far-v1"));
                Assert.That(root.transform.Find("DemoCanvas/OpponentFactionSelector/FactionLabel").GetComponent<UnityEngine.UI.Text>().text, Is.EqualTo("敌方 · 末地"));
                Assert.That(root.transform.Find("DemoCanvas/OpponentHUD/Name").GetComponent<UnityEngine.UI.Text>().text, Is.EqualTo("虚空行者"));

                GameObject.Find("Faction_nether").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Assert.That(root.transform.Find("DemoCanvas/PlayerHUD/Name").GetComponent<UnityEngine.UI.Text>().text, Is.EqualTo("熔岩统御者"));
                root.transform.Find("DemoCanvas/HandPlate/HandCards/Card_nt_006").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                var implementedCast = root.transform.Find("DemoCanvas/CardDetailsPanel/InspectorContent/Cast").GetComponent<UnityEngine.UI.Button>();
                Assert.That(implementedCast.interactable, Is.True);
                Assert.That(implementedCast.GetComponentInChildren<UnityEngine.UI.Text>().text, Is.EqualTo("释放卡牌"));
                Assert.That(root.transform.Find("DemoCanvas/CardDetailsPanel/InspectorContent/Implementation").GetComponent<UnityEngine.UI.Text>().text, Does.Contain("已接入"));

                GameObject.Find("Faction_snow_ice").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                root.transform.Find("DemoCanvas/HandPlate/HandCards/Card_si_001").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                var targetCast = root.transform.Find("DemoCanvas/CardDetailsPanel/InspectorContent/Cast").GetComponent<UnityEngine.UI.Button>();
                Assert.That(targetCast.GetComponentInChildren<UnityEngine.UI.Text>().text, Is.EqualTo("选择敌方目标"));
                targetCast.onClick.Invoke();
                Assert.That(root.transform.Find("DemoCanvas/CardDetailsPanel/InspectorContent/Cast").GetComponentInChildren<UnityEngine.UI.Text>().text, Is.EqualTo("取消目标选择"));
                Assert.That(opponentUnitMarker.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_HighlightStrength"), Is.GreaterThan(0f));

                var playerUnit = battlefield.GetSlotReferencePosition(true, DemoSlotKind.Unit, 0);
                var opponentUnit = battlefield.GetSlotReferencePosition(false, DemoSlotKind.Unit, 0);
                Assert.That(playerUnit.y, Is.LessThan(opponentUnit.y));
                Assert.That(playerUnit, Is.Not.EqualTo(opponentUnit));
                Assert.That(DemoMinecraftModelFactory.TryGetTextureKey("pf_001", out var beeTexture), Is.True);
                Assert.That(beeTexture, Is.EqualTo("entity_bee"));
                Assert.That(DemoMinecraftModelFactory.TryGetTextureKey("nt_003", out var blazeTexture), Is.True);
                Assert.That(blazeTexture, Is.EqualTo("entity_blaze"));
                Assert.That(DemoMinecraftModelFactory.TryGetTextureKey("si_003", out var strayTexture), Is.True);
                Assert.That(strayTexture, Is.EqualTo("entity_stray"));
                Assert.That(DemoMinecraftModelFactory.TryGetTextureKey("tk_014", out var smallMagmaTexture), Is.True);
                Assert.That(smallMagmaTexture, Is.EqualTo("entity_magma_cube"));

                var piecesRoot = root.transform.Find("BattlefieldPieces");
                battlefield.SyncPieces(new[]
                {
                    new DemoBattlefieldObject
                    {
                        InstanceId = "object-render-1", CardId = "pf_005", Player = true,
                        SlotKind = DemoSlotKind.Building, SlotIndex = 0, OccupiedSlots = 1, Health = 4, MaxHealth = 4
                    },
                    new DemoBattlefieldObject
                    {
                        InstanceId = "object-render-2", CardId = "pf_005", Player = true,
                        SlotKind = DemoSlotKind.Building, SlotIndex = 1, OccupiedSlots = 1, Health = 4, MaxHealth = 4
                    }
                }, System.Array.Empty<DemoBattlefieldObject>(), registry);
                Assert.That(piecesRoot.childCount, Is.EqualTo(2), "adjacent copies of one building card remain separate stable objects");
                Assert.That(piecesRoot.Find("Piece_object-render-1_pf_005"), Is.Not.Null);
                Assert.That(piecesRoot.Find("Piece_object-render-2_pf_005"), Is.Not.Null);

                battlefield.SyncPieces(new[]
                {
                    new DemoBattlefieldObject
                    {
                        InstanceId = "object-render-3", CardId = "db_007", Player = true,
                        SlotKind = DemoSlotKind.Building, SlotIndex = 0, OccupiedSlots = 2, Health = 8, MaxHealth = 8
                    }
                }, System.Array.Empty<DemoBattlefieldObject>(), registry);
                Assert.That(piecesRoot.childCount, Is.EqualTo(1), "one multi-slot structure produces one world object");
                var structurePiece = piecesRoot.Find("Piece_object-render-3_db_007");
                var expectedStructureCenter = (battlefield.GetSlotWorldPosition(true, DemoSlotKind.Building, 0) +
                                               battlefield.GetSlotWorldPosition(true, DemoSlotKind.Building, 1)) * 0.5f;
                Assert.That(structurePiece, Is.Not.Null);
                Assert.That(structurePiece.localPosition.x, Is.EqualTo(expectedStructureCenter.x).Within(0.001f));

                var buildingMarker1 = root.transform.Find("BattlefieldGeometry/SlotMarker_Player_Building_1/InteractiveGround");
                var buildingMarker2 = root.transform.Find("BattlefieldGeometry/SlotMarker_Player_Building_2/InteractiveGround");
                battlefield.SetSlotState(true, DemoSlotKind.Building, 0, true, false);
                battlefield.SetSlotState(true, DemoSlotKind.Building, 1, true, false);
                battlefield.SetSlotRangeHovered(true, DemoSlotKind.Building, 0, 2, true, false);
                Assert.That(buildingMarker.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_HighlightStrength"), Is.EqualTo(0.78f).Within(0.001f));
                Assert.That(buildingMarker1.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_HighlightStrength"), Is.EqualTo(0.78f).Within(0.001f));
                battlefield.SetSlotRangeHovered(true, DemoSlotKind.Building, 0, 2, false, false);
                battlefield.SetSlotRangeHovered(true, DemoSlotKind.Building, 2, 2, true, true);
                Assert.That(buildingMarker2.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_HighlightStrength"), Is.GreaterThanOrEqualTo(0.84f));
                battlefield.SetSlotRangeHovered(true, DemoSlotKind.Building, 2, 2, false, true);

                battlefield.SetSlotState(true, DemoSlotKind.Unit, 0, true, false);
                battlefield.SetSlotHovered(true, DemoSlotKind.Unit, 0, true);
                Assert.That(unitMarker.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_HighlightStrength"), Is.EqualTo(0.78f).Within(0.001f));
                Assert.That(unitRiser.GetComponent<MeshRenderer>().enabled, Is.True);
                battlefield.SetSlotPressed(true, DemoSlotKind.Unit, 0, true);
                Assert.That(unitMarker.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_HighlightStrength"), Is.EqualTo(0.92f).Within(0.001f));
                Assert.That(unitRiser.GetComponent<MeshRenderer>().enabled, Is.False);
                battlefield.SetSlotPressed(true, DemoSlotKind.Unit, 0, false);
                battlefield.SetSlotState(true, DemoSlotKind.Unit, 0, false, true);
                Assert.That(unitMarker.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_HighlightStrength"), Is.Zero);
                Assert.That(unitRiser.GetComponent<MeshRenderer>().enabled, Is.False);
                battlefield.SetSlotState(false, DemoSlotKind.Unit, 0, true, true);
                battlefield.SetSlotHovered(false, DemoSlotKind.Unit, 0, true);
                Assert.That(opponentUnitMarker.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_HighlightStrength"), Is.EqualTo(0.78f).Within(0.001f));
                endTurn.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Assert.That(endTurn.GetComponentInChildren<UnityEngine.UI.Text>().text, Is.EqualTo("结束回合"));
                Assert.That(root.transform.Find("DemoCanvas/RoundPlate/Round").GetComponent<UnityEngine.UI.Text>().text, Does.Contain("战斗"));
                Assert.That(root.transform.Find("DemoCanvas/CardDetailsPanel/InspectorContent/CombatHint"), Is.Not.Null);
                Assert.That(root.transform.Find("DemoCanvas/CardDetailsPanel/InspectorContent/Header").GetComponent<UnityEngine.UI.Text>().text, Is.EqualTo("战斗指令"));
                var combatHand = root.transform.Find("DemoCanvas/HandPlate/HandCards").GetComponent<CanvasGroup>();
                Assert.That(combatHand.alpha, Is.EqualTo(0.52f).Within(0.001f));
                Assert.That(combatHand.blocksRaycasts, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CardUiShowsARegisteredDiscountWithoutChangingBaseCost()
        {
            var root = new GameObject("DiscountedCardTest", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(CardUI));
            try
            {
                var card = root.GetComponent<CardUI>();
                card.Bind(CardContentLoader.Load(), "db_005", new Vector2(158, 216), true, null, null, 2);

                Assert.That(card.BaseCost, Is.EqualTo(3));
                Assert.That(card.DisplayedCost, Is.EqualTo(2));
                Assert.That(root.transform.Find("Cost").GetComponent<UnityEngine.UI.Text>().text, Is.EqualTo("2"));
                Assert.That(root.transform.Find("CostModifierBadge").GetComponent<UnityEngine.UI.Image>().type, Is.EqualTo(UnityEngine.UI.Image.Type.Tiled));
                Assert.That(root.transform.Find("CostModifier").GetComponent<UnityEngine.UI.Text>().text, Is.EqualTo("-1"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PowderSnowUsesTargetingAndExpiresAfterTheSkippedOpponentTurn()
        {
            var registry = CardContentLoader.Load();
            var match = new DemoLocalMatch();
            match.ResetHand(new[] { "si_006" });
            Assert.That(registry.TryGetDefinition("si_006", out var powderSnow), Is.True);
            Assert.That(powderSnow.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));
            Assert.That(registry.TryGetDefinition("nt_003", out var blaze), Is.True);
            match.ResetOpponent(new[] { blaze });
            var target = match.GetObject(false, DemoSlotKind.Unit, 0);

            var missing = match.ApplyPlayCard(powderSnow, match.CreatePlayCardCommand("si_006"));
            Assert.That(missing.Accepted, Is.False);
            Assert.That(match.Hand, Does.Contain("si_006"));

            var played = match.ApplyPlayCard(powderSnow,
                match.CreatePlayCardCommand("si_006", "UNIT", target.InstanceId));
            Assert.That(played.Accepted, Is.True);
            Assert.That(target.Attack, Is.EqualTo(1));
            Assert.That(target.HasStatus("SLOW"), Is.True);
            Assert.That(target.Statuses.Single().remainingDuration, Is.EqualTo(1));

            match.EndPlayerTurn();
            Assert.That(target.HasStatus("SLOW"), Is.True, "Slow belongs to the enemy and must not expire at the caster end phase.");
            match.BeginNextPlayerTurn();
            Assert.That(target.HasStatus("SLOW"), Is.False);
            Assert.That(target.Attack, Is.EqualTo(3));
        }

        [Test]
        public void StrayRequiresADeploymentTargetAndSharesSlowWithoutStackingTheBucketPenalty()
        {
            var registry = CardContentLoader.Load();
            var match = new DemoLocalMatch();
            match.ResetHand(new[] { "si_003", "si_006" });
            Assert.That(registry.TryGetDefinition("si_003", out var stray), Is.True);
            Assert.That(stray.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));
            Assert.That(registry.TryGetDefinition("si_006", out var powderSnow), Is.True);
            Assert.That(registry.TryGetDefinition("pf_001", out var bee), Is.True);
            match.ResetOpponent(new[] { bee });
            var target = match.GetObject(false, DemoSlotKind.Unit, 0);

            var missing = match.ApplyDeploy(stray, match.CreateDeployCommand("si_003", DemoSlotKind.Unit, 0));
            Assert.That(missing.Accepted, Is.False);
            Assert.That(missing.Code, Is.EqualTo(DemoCommandRejectionCode.InvalidTarget));
            Assert.That(match.UnitSlots[0], Is.Null.Or.Empty);
            Assert.That(match.Hand, Does.Contain("si_003"));

            var deployed = match.ApplyDeploy(stray,
                match.CreateDeployCommand("si_003", DemoSlotKind.Unit, 0, MatchPaymentMethods.Redstone, "UNIT", target.InstanceId));
            Assert.That(deployed.Accepted, Is.True);
            Assert.That(match.GetObject(true, DemoSlotKind.Unit, 0).CardId, Is.EqualTo("si_003"));
            Assert.That(target.HasStatus("SLOW"), Is.True);
            Assert.That(target.Statuses.Single().sourceInstanceId, Is.EqualTo(match.GetObject(true, DemoSlotKind.Unit, 0).InstanceId));
            Assert.That(target.Statuses.Single().attackModifier, Is.Zero);

            var bucket = match.ApplyPlayCard(powderSnow,
                match.CreatePlayCardCommand("si_006", "UNIT", target.InstanceId));
            Assert.That(bucket.Accepted, Is.True);
            Assert.That(target.Attack, Is.Zero);
            Assert.That(target.Statuses.Single().attackModifier, Is.EqualTo(-1));
            Assert.That(target.Statuses.Single().boundAttackModifier, Is.EqualTo(-2));
            Assert.That(target.Statuses.Single().sourceCardId, Is.EqualTo("si_006"));
            Assert.That(target.Statuses.Single().sourceInstanceId, Is.Empty);

            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            Assert.That(target.Attack, Is.EqualTo(1));
            Assert.That(target.Statuses, Is.Empty);
        }

        [Test]
        public void RiptideTridentEquipsHeroAndMovesASurvivingTargetToAnAdjacentWorldSlot()
        {
            var registry = CardContentLoader.Load();
            var match = new DemoLocalMatch();
            match.ResetHand(new[] { "or_006" });
            Assert.That(registry.TryGetDefinition("or_006", out var trident), Is.True);
            Assert.That(trident.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));
            Assert.That(registry.TryGetDefinition("si_005", out var polarBear), Is.True);
            match.ResetOpponent(new[] { polarBear, polarBear });

            var equipped = match.ApplyPlayCard(trident, match.CreatePlayCardCommand("or_006"));
            Assert.That(equipped.Accepted, Is.True);
            Assert.That(match.PlayerEquipment.CardId, Is.EqualTo("or_006"));
            Assert.That(match.PlayerEquipment.Durability, Is.EqualTo(3));
            Assert.That(match.ApplyEnterCombat(match.CreateEnterCombatCommand()).Accepted, Is.True);

            var target = match.GetObject(false, DemoSlotKind.Unit, 2);
            var attacked = match.ApplyAttack(match.CreateAttackCommand(MatchAttackerIds.Hero, "UNIT", target.InstanceId));
            Assert.That(attacked.Accepted, Is.True);
            Assert.That(match.PlayerHeroHasAttacked, Is.True);
            Assert.That(match.PlayerEquipment.Durability, Is.EqualTo(2));
            Assert.That(match.PlayerLife, Is.EqualTo(27));
            Assert.That(match.PendingChoice.kind, Is.EqualTo("MOVE_UNIT"));
            Assert.That(match.PendingChoice.options.Select(value => value.slotIndex), Is.EquivalentTo(new[] { 1, 3 }));

            var moved = match.ApplyResolveChoice(match.CreateResolveChoiceCommand(match.PendingChoice.choiceId, 0));
            Assert.That(moved.Accepted, Is.True);
            Assert.That(match.GetObject(false, DemoSlotKind.Unit, 1).InstanceId, Is.EqualTo(target.InstanceId));
            Assert.That(match.OpponentUnitSlots[2], Is.Null.Or.Empty);
        }

        [Test]
        public void SalmonSchoolMovesAcrossFriendlyWorldSlotsAndItsAttackBonusExpiresAtEndOfTurn()
        {
            var registry = CardContentLoader.Load();
            var match = new DemoLocalMatch();
            match.ResetHand(new[] { "or_001" });
            Assert.That(registry.TryGetDefinition("or_001", out var salmon), Is.True);
            Assert.That(salmon.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));

            var deployed = match.ApplyDeploy(salmon, match.CreateDeployCommand("or_001", DemoSlotKind.Unit, 1));
            Assert.That(deployed.Accepted, Is.True);
            Assert.That(match.PendingChoice.kind, Is.EqualTo("MOVE_UNIT"));
            Assert.That(match.PendingChoice.effectId, Is.EqualTo("effect.or_001.01"));
            Assert.That(match.PendingChoice.options.Select(value => value.slotIndex), Is.EquivalentTo(new[] { 0, 2 }));

            var unit = match.GetObject(true, DemoSlotKind.Unit, 1);
            var moved = match.ApplyResolveChoice(match.CreateResolveChoiceCommand(match.PendingChoice.choiceId, 1));
            Assert.That(moved.Accepted, Is.True);
            Assert.That(match.GetObject(true, DemoSlotKind.Unit, 2).InstanceId, Is.EqualTo(unit.InstanceId));
            Assert.That(unit.Attack, Is.EqualTo(2));
            Assert.That(unit.TemporaryAttackModifier, Is.EqualTo(1));

            match.EndPlayerTurn();
            Assert.That(unit.Attack, Is.EqualTo(1));
            Assert.That(unit.TemporaryAttackModifier, Is.Zero);
        }

        private static float ProjectedWidth(Camera camera, Transform surface, Vector3[] vertices)
        {
            var min = vertices.Min(vertex => camera.WorldToViewportPoint(surface.TransformPoint(vertex)).x);
            var max = vertices.Max(vertex => camera.WorldToViewportPoint(surface.TransformPoint(vertex)).x);
            return max - min;
        }
    }
}
