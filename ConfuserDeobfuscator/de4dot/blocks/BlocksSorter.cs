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

namespace de4dot.blocks {
	class BlocksSorter {
		ScopeBlock scopeBlock;

		class BlockInfo {
			public int dfsNumber = -1;
			public int low;
			public BaseBlock baseBlock;
			public bool onStack;

			public BlockInfo(BaseBlock baseBlock) {
				this.baseBlock = baseBlock;
			}

			public bool visited() {
				return dfsNumber >= 0;
			}

			public override string ToString() {
				return string.Format("L:{0}, D:{1}, S:{2}", low, dfsNumber, onStack);
			}
		}

		// It uses Tarjan's strongly connected components algorithm to find all SCCs.
		// See http://www.ics.uci.edu/~eppstein/161/960220.html or wikipedia for a good explanation.
		// The non-Tarjan code is still pretty simple and can (should) be improved.
		class Sorter {
			ScopeBlock scopeBlock;
			IList<BaseBlock> validBlocks;
			Dictionary<BaseBlock, BlockInfo> blockToInfo = new Dictionary<BaseBlock, BlockInfo>();
			Stack<BlockInfo> stack = new Stack<BlockInfo>();
			List<BaseBlock> sorted;
			int dfsNumber = 0;
			bool skipFirstBlock;
			BaseBlock firstBlock;

			public Sorter(ScopeBlock scopeBlock, IList<BaseBlock> validBlocks, bool skipFirstBlock) {
				this.scopeBlock = scopeBlock;
				this.validBlocks = validBlocks;
				this.skipFirstBlock = skipFirstBlock;
			}

			public List<BaseBlock> sort() {
				if (validBlocks.Count == 0)
					return new List<BaseBlock>();
				if (skipFirstBlock)
					firstBlock = validBlocks[0];

				foreach (var block in validBlocks) {
					if (block != firstBlock)
						blockToInfo[block] = new BlockInfo(block);
				}

				sorted = new List<BaseBlock>(validBlocks.Count);
				var finalList = new List<BaseBlock>(validBlocks.Count);

				if (firstBlock is Block) {
					foreach (var target in getTargets(firstBlock)) {
						visit(target);
						finalList.AddRange(sorted);
						sorted.Clear();
					}
				}
				foreach (var bb in validBlocks) {
					visit(bb);
					finalList.AddRange(sorted);
					sorted.Clear();
				}

				if (stack.Count > 0)
					throw new ApplicationException("Stack isn't empty");

				if (firstBlock != null)
					finalList.Insert(0, firstBlock);
				else if (validBlocks[0] != finalList[0]) {
					// Make sure the original first block is first
					int index = finalList.IndexOf(validBlocks[0]);
					finalList.RemoveAt(index);
					finalList.Insert(0, validBlocks[0]);
				}
				return finalList;
			}

			void visit(BaseBlock bb) {
				var info = getInfo(bb);
				if (info == null)
					return;
				if (info.baseBlock == firstBlock)
					return;
				if (info.visited())
					return;
				visit(info);
			}

			BlockInfo getInfo(BaseBlock baseBlock) {
				baseBlock = scopeBlock.toChild(baseBlock);
				if (baseBlock == null)
					return null;
				BlockInfo info;
				blockToInfo.TryGetValue(baseBlock, out info);
				return info;
			}

			List<BaseBlock> getTargets(BaseBlock baseBlock) {
				var list = new List<BaseBlock>();

				if (baseBlock is Block) {
					var block = (Block)baseBlock;
					addTargets(list, block.getTargets());
				}
				else if (baseBlock is TryBlock)
					addTargets(list, (TryBlock)baseBlock);
				else if (baseBlock is TryHandlerBlock)
					addTargets(list, (TryHandlerBlock)baseBlock);
				else
					addTargets(list, (ScopeBlock)baseBlock);

				return list;
			}

			void addTargets(List<BaseBlock> dest, TryBlock tryBlock) {
				addTargets(dest, (ScopeBlock)tryBlock);
				foreach (var tryHandlerBlock in tryBlock.TryHandlerBlocks) {
					dest.Add(tryHandlerBlock);
					addTargets(dest, tryHandlerBlock);
				}
			}

			void addTargets(List<BaseBlock> dest, TryHandlerBlock tryHandlerBlock) {
				addTargets(dest, (ScopeBlock)tryHandlerBlock);

				dest.Add(tryHandlerBlock.FilterHandlerBlock);
				addTargets(dest, tryHandlerBlock.FilterHandlerBlock);

				dest.Add(tryHandlerBlock.HandlerBlock);
				addTargets(dest, tryHandlerBlock.HandlerBlock);
			}

			void addTargets(List<BaseBlock> dest, ScopeBlock scopeBlock) {
				foreach (var block in scopeBlock.getAllBlocks())
					addTargets(dest, block.getTargets());
			}

			void addTargets(List<BaseBlock> dest, IEnumerable<Block> source) {
				var list = new List<Block>(source);
				list.Reverse();
				foreach (var block in list)
					dest.Add(block);
			}

			void visit(BlockInfo info) {
				if (info.baseBlock == firstBlock)
					throw new ApplicationException("Can't visit firstBlock");
				stack.Push(info);
				info.onStack = true;
				info.dfsNumber = dfsNumber;
				info.low = dfsNumber;
				dfsNumber++;

				foreach (var tmp in getTargets(info.baseBlock)) {
					var targetInfo = getInfo(tmp);
					if (targetInfo == null)
						continue;
					if (targetInfo.baseBlock == firstBlock)
						continue;

					if (!targetInfo.visited()) {
						visit(targetInfo);
						info.low = Math.Min(info.low, targetInfo.low);
					}
					else if (targetInfo.onStack)
						info.low = Math.Min(info.low, targetInfo.dfsNumber);
				}

				if (info.low != info.dfsNumber)
					return;
				var sccBlocks = new List<BaseBlock>();
				while (true) {
					var poppedInfo = stack.Pop();
					poppedInfo.onStack = false;
					sccBlocks.Add(poppedInfo.baseBlock);
					if (ReferenceEquals(info, poppedInfo))
						break;
				}
				if (sccBlocks.Count > 1) {
					sccBlocks.Reverse();
					var result = new Sorter(scopeBlock, sccBlocks, true).sort();
					sortLoopBlock(result);
					sorted.InsertRange(0, result);
				}
				else {
					sorted.Insert(0, sccBlocks[0]);
				}
			}

			void sortLoopBlock(List<BaseBlock> list) {
				// Some popular decompilers sometimes produce bad output unless the loop condition
				// checker block is at the end of the loop. Eg., they may use a while loop when
				// it's really a for/foreach loop.

				var loopStart = getLoopStartBlock(list);
				if (loopStart == null)
					return;

				if (!list.Remove(loopStart))
					throw new ApplicationException("Could not remove block");
				list.Add(loopStart);
			}

			Block getLoopStartBlock(List<BaseBlock> list) {
				var loopBlocks = new Dictionary<Block, bool>(list.Count);
				foreach (var bb in list) {
					var block = bb as Block;
					if (block != null)
						loopBlocks[block] = true;
				}

				var targetBlocks = new Dictionary<Block, int>();
				foreach (var bb in list) {
					var block = bb as Block;
					if (block == null)
						continue;
					foreach (var source in block.Sources) {
						if (loopBlocks.ContainsKey(source))
							continue;
						int count;
						targetBlocks.TryGetValue(block, out count);
						targetBlocks[block] = count + 1;
					}
				}

				int max = -1;
				Block loopStart = null;
				foreach (var kv in targetBlocks) {
					if (kv.Value <= max)
						continue;
					max = kv.Value;
					loopStart = kv.Key;
				}

				return loopStart;
			}
		}

		public BlocksSorter(ScopeBlock scopeBlock) {
			this.scopeBlock = scopeBlock;
		}

		public List<BaseBlock> sort() {
			var sorted = new Sorter(scopeBlock, scopeBlock.BaseBlocks, false).sort();
			return new ForwardScanOrder(scopeBlock, sorted).fix();
		}
	}
}
