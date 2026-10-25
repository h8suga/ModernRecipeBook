using System;
using System.Collections.Generic;
using System.Linq;

namespace ModernRecipeBook;

internal static class BookBatchPlanner
{
	private sealed class Dinic
	{
		internal sealed class Edge(int to, int reverse, long capacity)
		{
			internal readonly int To = to;

			internal readonly int Reverse = reverse;

			internal long Capacity = capacity;
		}

		private readonly List<Edge>[] graph;

		private readonly int[] level;

		private readonly int[] next;

		internal Dinic(int nodes)
		{
			graph = (from _ in Enumerable.Range(0, nodes)
				select new List<Edge>()).ToArray();
			level = new int[nodes];
			next = new int[nodes];
		}

		internal Edge AddEdge(int from, int to, long capacity)
		{
			Edge forward = new Edge(to, graph[to].Count, Math.Max(0L, capacity));
			Edge reverse = new Edge(from, graph[from].Count, 0L);
			graph[from].Add(forward);
			graph[to].Add(reverse);
			return forward;
		}

		internal long MaxFlow(int source, int sink, long stopAt)
		{
			long total = 0L;
			while (total < stopAt && BuildLevels(source, sink))
			{
				Array.Clear(next);
				long sent;
				for (; total < stopAt; total += sent)
				{
					if ((sent = Send(source, sink, stopAt - total)) <= 0)
					{
						break;
					}
				}
			}
			return total;
		}

		private bool BuildLevels(int source, int sink)
		{
			Array.Fill(level, -1);
			Queue<int> queue = new Queue<int>();
			level[source] = 0;
			queue.Enqueue(source);
			while (queue.Count > 0)
			{
				int node = queue.Dequeue();
				foreach (Edge edge in graph[node])
				{
					if (edge.Capacity > 0 && level[edge.To] < 0)
					{
						level[edge.To] = level[node] + 1;
						queue.Enqueue(edge.To);
					}
				}
			}
			return level[sink] >= 0;
		}

		private long Send(int node, int sink, long amount)
		{
			if (node == sink)
			{
				return amount;
			}
			while (next[node] < graph[node].Count)
			{
				Edge edge = graph[node][next[node]];
				if (edge.Capacity > 0 && level[edge.To] == level[node] + 1)
				{
					long sent = Send(edge.To, sink, Math.Min(amount, edge.Capacity));
					if (sent > 0)
					{
						edge.Capacity -= sent;
						graph[edge.To][edge.Reverse].Capacity += sent;
						return sent;
					}
				}
				next[node]++;
			}
			return 0L;
		}
	}

	private const int MaxNeeds = 256;

	private const int MaxTypes = 4096;

	private const int MaxAlternatives = 16384;

	internal static int MaxBatches(IReadOnlyList<BookBatchNeed> needs, IReadOnlyDictionary<int, int> available, int limit)
	{
		if (limit <= 0)
		{
			return 0;
		}
		if (needs == null || needs.Count == 0)
		{
			return limit;
		}
		if (needs.Count > 256)
		{
			return 1;
		}
		HashSet<int> boundedTypes = new HashSet<int>();
		int alternatives = 0;
		foreach (BookBatchNeed need2 in needs)
		{
			foreach (int type in need2.AcceptedTypes)
			{
				if (++alternatives > 16384)
				{
					return 1;
				}
				if (type > 0 && boundedTypes.Add(type) && boundedTypes.Count > 4096)
				{
					return 1;
				}
			}
		}
		int low = 0;
		int high = limit;
		while (low < high)
		{
			int middle = low + (high - low + 1) / 2;
			if (CanAllocate(needs, available, middle))
			{
				low = middle;
			}
			else
			{
				high = middle - 1;
			}
		}
		return low;
	}

	internal static long[] AllocateMaterials(IReadOnlyList<long> quantities, IReadOnlyList<IReadOnlyCollection<int>> accepted, IReadOnlyDictionary<int, long> available)
	{
		long[] result = new long[quantities.Count];
		if (quantities.Count != accepted.Count)
		{
			return result;
		}
		int[] types = (from t in (from t in accepted.SelectMany((IReadOnlyCollection<int> a) => a)
				where t > 0
				select t).Distinct()
			orderby t
			select t).ToArray();
		long total = quantities.Sum((long q) => Math.Max(0L, q));
		int itemOffset = 1;
		int needOffset = 1 + types.Length;
		int sink = needOffset + quantities.Count;
		Dinic flow = new Dinic(sink + 1);
		Dictionary<int, int> nodes = new Dictionary<int, int>();
		for (int i = 0; i < types.Length; i++)
		{
			nodes[types[i]] = itemOffset + i;
			flow.AddEdge(0, itemOffset + i, Math.Min(total, Math.Max(0L, available.GetValueOrDefault(types[i]))));
		}
		Dinic.Edge[] ends = new Dinic.Edge[quantities.Count];
		for (int i2 = 0; i2 < quantities.Count; i2++)
		{
			long demand = Math.Max(0L, quantities[i2]);
			ends[i2] = flow.AddEdge(needOffset + i2, sink, demand);
			foreach (int type in accepted[i2].Distinct())
			{
				if (nodes.TryGetValue(type, out var node))
				{
					flow.AddEdge(node, needOffset + i2, demand);
				}
			}
		}
		flow.MaxFlow(0, sink, total);
		for (int i3 = 0; i3 < result.Length; i3++)
		{
			result[i3] = Math.Max(0L, quantities[i3]) - ends[i3].Capacity;
		}
		return result;
	}

	// Splits needs over layers of sources (the inventory, then nearby banks, then remote containers) and returns how
	// much of each type every layer gives, or null when all layers together fall short. The flow is first run on the
	// first layer alone and then widened layer by layer; a source edge's flow never drops when its capacity grows, so
	// the final use of each type is taken from the earlier layers first.
	internal static Dictionary<int, long>[] PlanLayers(IReadOnlyList<long> quantities, IReadOnlyList<IReadOnlyCollection<int>> accepted, IReadOnlyList<IReadOnlyDictionary<int, long>> layers)
	{
		if (quantities.Count != accepted.Count || layers.Count == 0)
		{
			return null;
		}
		int[] types = accepted.SelectMany((IReadOnlyCollection<int> a) => a).Where((int t) => t > 0).Distinct().OrderBy((int t) => t).ToArray();
		long total = quantities.Sum((long q) => Math.Max(0L, q));
		int needOffset = 1 + types.Length;
		int sink = needOffset + quantities.Count;
		Dinic flow = new Dinic(sink + 1);
		Dictionary<int, int> nodes = new Dictionary<int, int>();
		Dinic.Edge[] sources = new Dinic.Edge[types.Length];
		long[] given = new long[types.Length];
		for (int i = 0; i < types.Length; i++)
		{
			nodes[types[i]] = 1 + i;
			given[i] = Math.Min(total, Math.Max(0L, layers[0].GetValueOrDefault(types[i])));
			sources[i] = flow.AddEdge(0, 1 + i, given[i]);
		}
		for (int n = 0; n < quantities.Count; n++)
		{
			long demand = Math.Max(0L, quantities[n]);
			flow.AddEdge(needOffset + n, sink, demand);
			foreach (int type in accepted[n].Distinct())
			{
				if (nodes.TryGetValue(type, out var node))
				{
					flow.AddEdge(node, needOffset + n, demand);
				}
			}
		}
		long flowed = flow.MaxFlow(0, sink, total);
		for (int layer = 1; layer < layers.Count && flowed < total; layer++)
		{
			for (int i = 0; i < types.Length; i++)
			{
				long add = Math.Min(total, Math.Max(0L, layers[layer].GetValueOrDefault(types[i])));
				sources[i].Capacity += add;
				given[i] += add;
			}
			flowed += flow.MaxFlow(0, sink, total - flowed);
		}
		if (flowed < total)
		{
			return null;
		}
		Dictionary<int, long>[] result = new Dictionary<int, long>[layers.Count];
		for (int layer = 0; layer < layers.Count; layer++)
		{
			result[layer] = new Dictionary<int, long>();
		}
		for (int i = 0; i < types.Length; i++)
		{
			long used = given[i] - sources[i].Capacity;
			for (int layer = 0; layer < layers.Count && used > 0; layer++)
			{
				long take = Math.Min(used, Math.Max(0L, layers[layer].GetValueOrDefault(types[i])));
				if (take > 0)
				{
					result[layer][types[i]] = take;
					used -= take;
				}
			}
		}
		return result;
	}

	private static bool CanAllocate(IReadOnlyList<BookBatchNeed> needs, IReadOnlyDictionary<int, int> available, int batches)
	{
		int[] types = (from result in (from num in needs.SelectMany((BookBatchNeed bookBatchNeed) => bookBatchNeed.AcceptedTypes)
				where num > 0
				select num).Distinct()
			orderby result
			select result).ToArray();
		if (types.Length == 0)
		{
			return false;
		}
		int source = 0;
		int itemOffset = 1;
		int needOffset = itemOffset + types.Length;
		int sink = needOffset + needs.Count;
		Dinic flow = new Dinic(sink + 1);
		Dictionary<int, int> itemNodes = new Dictionary<int, int>(types.Length);
		for (int i = 0; i < types.Length; i++)
		{
			int type = types[i];
			itemNodes[type] = itemOffset + i;
			available.TryGetValue(type, out var count);
			flow.AddEdge(source, itemOffset + i, Math.Max(0, count));
		}
		long totalDemand = 0L;
		for (int i2 = 0; i2 < needs.Count; i2++)
		{
			BookBatchNeed need = needs[i2];
			if (need.Quantity <= 0)
			{
				continue;
			}
			long demand = (long)need.Quantity * (long)batches;
			totalDemand += demand;
			int node = needOffset + i2;
			flow.AddEdge(node, sink, demand);
			foreach (int type2 in need.AcceptedTypes.Distinct())
			{
				if (itemNodes.TryGetValue(type2, out var itemNode))
				{
					flow.AddEdge(itemNode, node, demand);
				}
			}
		}
		if (totalDemand == 0L)
		{
			return true;
		}
		return flow.MaxFlow(source, sink, totalDemand) == totalDemand;
	}
}
