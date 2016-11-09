using Microsoft.VisualStudio.TestTools.UnitTesting;
using STM;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TransactionalAction = System.Action<STM.IStmTransaction>;

namespace STM_TESTS
{

    [TestClass]
    public class STMTester
    {

        private static string logFileName = "Log.txt";

        [TestMethod]
        public void TestTransactionConflict()
        {
            File.WriteAllText(logFileName, string.Empty);
            var variable_1 = new StmMemory<int>();
            var variable_2 = new StmMemory<int>();
            File.WriteAllText("Log.txt", string.Empty);
            List<Action> actions = new List<Action>()
            {
                new Action(() => {
                    Stm.Do(new TransactionalAction((IStmTransaction transaction) => {
                        variable_1.Set(2, transaction);
                        variable_1.Version[0] = 15;
                        variable_2.Set(3, transaction);
                    }), false);
                }),
            };
            List<Task> tasks = new List<Task>();
            foreach (Action action in actions)
            {
                tasks.Add(Task.Run(action));
            }
            foreach (Task task in tasks)
            {
                task.Wait();
            }
            string[] logFileLines = File.ReadAllLines("Log.txt");
            string lastLogLine = logFileLines[logFileLines.Length - 2];
            Assert.AreEqual(true, lastLogLine.Contains(I_STM_TRANSACTION_STATE.CONFLICT.ToString()));
        }

        enum TRANSACTION_ACTION { TRY_COMMIT, COMMIT, CONFLICT };

        [TestMethod]
        public void TestCorrectSTMWork()
        {
            File.WriteAllText(logFileName, string.Empty);
            var variable_1 = new StmMemory<int>();
            var variable_2 = new StmMemory<int>();
            File.WriteAllText("Log.txt", string.Empty);
            List<Action> actions = new List<Action>()
            {
                new Action(() => {
                    Stm.Do(new TransactionalAction((IStmTransaction transaction) => {
                        variable_1.Set(2, transaction);
                        variable_2.Set(3, transaction);
                    }));
                }),
                new Action(() => {
                    Stm.Do(new TransactionalAction((IStmTransaction transaction) => {
                        variable_1.Set(3, transaction);
                        variable_2.Set(4, transaction);
                    }));
                }),
                new Action(() => {
                    Stm.Do(new TransactionalAction((IStmTransaction transaction) => {
                        variable_1.Get(transaction);
                        variable_2.Get(transaction);
                    }));
                }),
            };
            List<Task> tasks = new List<Task>();
            foreach (Action action in actions)
            {
                tasks.Add(Task.Run(action));
            }
            foreach (Task task in tasks)
            {
                task.Wait();
            }
            List<KeyValuePair<int, TRANSACTION_ACTION>> actionsSequence = GetTransactionActionsSequence();
            TestActionCount(actionsSequence, TRANSACTION_ACTION.COMMIT, 3);
            TestActionCount(actionsSequence, TRANSACTION_ACTION.CONFLICT, 2 + 1, true);
            TestTransactionsActionsSequence(actionsSequence);
        }

        private List<KeyValuePair<int, TRANSACTION_ACTION>> GetTransactionActionsSequence()
        {
            List<KeyValuePair<int, TRANSACTION_ACTION>> actionsSequence = new List<KeyValuePair<int, TRANSACTION_ACTION>>();
            Regex regEx = new Regex(@"TRANSACTION[ ]+(\d)");
            string[] logFileLines = File.ReadAllLines(logFileName);
            for (int i = 0; i < logFileLines.Length; i++)
            {
                string line = logFileLines[i];
                int transactionNumber;
                Match match = regEx.Match(line);
                int.TryParse(match.Groups[1].Value, out transactionNumber);
                if (line.Contains("TryCommit"))
                {
                    actionsSequence.Add(new KeyValuePair<int, TRANSACTION_ACTION>(transactionNumber, TRANSACTION_ACTION.TRY_COMMIT));
                }
                else
                {
                    if (line.Contains(I_STM_TRANSACTION_STATE.COMMITED.ToString()))
                    {
                        actionsSequence.Add(new KeyValuePair<int, TRANSACTION_ACTION>(transactionNumber, TRANSACTION_ACTION.COMMIT));
                    }
                    else
                    {
                        if (line.Contains(I_STM_TRANSACTION_STATE.CONFLICT.ToString()))
                        {
                            actionsSequence.Add(new KeyValuePair<int, TRANSACTION_ACTION>(transactionNumber, TRANSACTION_ACTION.CONFLICT));
                        }
                    }
                }
            }
            return actionsSequence;
        }

        private void TestActionCount(List<KeyValuePair<int, TRANSACTION_ACTION>> actionsSequence, TRANSACTION_ACTION testAction, int expectedCount, bool canBeLess = false)
        {
            int actionCount = 0;
            foreach (KeyValuePair<int, TRANSACTION_ACTION> action in actionsSequence)
            {
                if (action.Value == testAction)
                {
                    actionCount++;
                }
            }
            if(canBeLess)
            {
                Assert.AreEqual(true, actionCount <= expectedCount);
            }
            else
            {
                Assert.AreEqual(expectedCount, actionCount);
            }
        }

        private void TestTransactionsActionsSequence(List<KeyValuePair<int, TRANSACTION_ACTION>> actionsSequence)
        {
            int transactionThatCommits = -1;
            List<int> transactionsThatNextActionShouldBeConflict = new List<int>();
            foreach (KeyValuePair<int, TRANSACTION_ACTION> action in actionsSequence)
            {
                if (action.Key == transactionThatCommits)
                {
                    Assert.AreEqual(true, action.Value == TRANSACTION_ACTION.COMMIT || action.Value == TRANSACTION_ACTION.CONFLICT);
                    transactionThatCommits = -1;
                }
                if (transactionsThatNextActionShouldBeConflict.Contains(action.Key))
                {
                    Assert.AreEqual(TRANSACTION_ACTION.CONFLICT, action.Value);
                    transactionsThatNextActionShouldBeConflict.Remove(action.Key);
                }
                else
                {
                    if (action.Value == TRANSACTION_ACTION.TRY_COMMIT)
                    {
                        if (transactionThatCommits != -1)
                        {
                            transactionsThatNextActionShouldBeConflict.Add(action.Key);
                        }
                        else
                        {
                            transactionThatCommits = action.Key;
                        }
                    }
                }
            }
        }

    }

}