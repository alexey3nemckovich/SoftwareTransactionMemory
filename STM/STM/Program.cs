using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using TransactionalAction = System.Action<STM.ITransaction>;

namespace STM
{

    class Program
    {

        static void Main(string[] args)
        {
            Stm.Init();
            IStmMemory variable_1 = new StmMemory<int>();
            IStmMemory variable_2 = new StmMemory<int>();
            File.WriteAllText("Log.txt", string.Empty);
            List<Action> actions = new List<Action>()
            {
                //new Action(() => {
                //    Stm.Do(new TransactionalAction((IStmTransaction transaction) => {
                //        variable_1.Set(2, transaction);
                //        variable_2.Set(3, transaction);
                //    }));
                //}),
                //new Action(() => {
                //    Stm.Do(new TransactionalAction((IStmTransaction transaction) => {
                //        variable_1.Set(3, transaction);
                //        variable_2.Set(4, transaction);
                //    }));
                //}),
                //new Action(() => {
                //    Stm.Do(new TransactionalAction((IStmTransaction transaction) => {
                //        variable_1.Get(transaction);
                //        variable_2.Get(transaction);
                //    }));
                //}),
                new Action(() => {
                    Stm.Do(new TransactionalAction((ITransaction transaction) => {
                        List<Action> subActions = new List<Action>();
                        variable_1.Set(transaction, 1);
                        Action subTransaction1 = new Action(() => {
                                Stm.Do(new TransactionalAction((ITransaction subTransaction) => {
                                    subTransaction.SetParentTransaction(transaction);
                                    variable_1.Set(subTransaction, 2);
                                    variable_2.Set(subTransaction, 3);
                            }));
                        });
                        //Action subTransaction2 = new Action(() => {
                        //        Stm.Do(new TransactionalAction((IStmTransaction subTransaction) => {
                        //            subTransaction.SetParentTransaction(transaction);
                        //            variable_1.Set(2, subTransaction);
                        //            variable_2.Set(3, subTransaction);
                        //    }));
                        //});
                        subActions.Add(subTransaction1);
                        //subActions.Add(subTransaction2);
                        List<Task> subTasks = new List<Task>();
                        foreach(Action action in subActions)
                        {
                            subTasks.Add(Task.Run(action));
                        }
                        foreach(Task task in subTasks)
                        {
                            task.Wait();
                        }
                        variable_1.Get(transaction);
                    }));
                }),
            };
            List<Task> tasks = new List<Task>();
            foreach(Action action in actions)
            {
                tasks.Add(Task.Run(action));
            }
            foreach (Task task in tasks)
            {
                task.Wait();
            }
        }

    }

}