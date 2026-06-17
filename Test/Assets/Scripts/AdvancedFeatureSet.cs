using Firebase.Database;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

public class AdvancedUserState
{
    public string UserKey;
    public string NickName;
    public int Coin;
    public int Score;
    public Dictionary<string, bool> UnitList = AdvancedDataUtility.CreateDefaultUnitList();
    public Dictionary<string, int> Inventory = AdvancedDataUtility.CreateDefaultInventory();
}

public class AuctionItemData
{
    public string Key;
    public string SellerKey;
    public string SellerNickName;
    public string ItemName;
    public int Price;
}

public class UnitPurchaseFeature
{
    readonly DatabaseReference reference;

    public UnitPurchaseFeature(DatabaseReference reference)
    {
        this.reference = reference;
    }

    public void BuyUnit(string userKey, string unitName, int price, Action<AdvancedUserState, string> onSuccess, Action<string> onFailure)
    {
        if (string.IsNullOrEmpty(userKey))
        {
            onFailure("로그인 정보가 없습니다.");
            return;
        }

        reference.Child("UserInfo").Child(userKey).GetValueAsync().ContinueWith(task =>
        {
            if (task.IsFaulted || task.IsCanceled || !task.Result.Exists)
            {
                onFailure("유닛 구매를 위한 유저 데이터 불러오기 실패");
                return;
            }

            DataSnapshot snapshot = task.Result;
            int coin = AdvancedDataUtility.ReadInt(snapshot.Child("Coin"), 0);
            Dictionary<string, bool> unitList = AdvancedDataUtility.ParseUnitList(AdvancedDataUtility.ReadString(snapshot.Child("UnitList")));

            if (unitList.ContainsKey(unitName) && unitList[unitName])
            {
                onFailure(unitName + "은 이미 보유 중입니다.");
                return;
            }

            if (coin < price)
            {
                onFailure(unitName + " 구매에 필요한 코인이 부족합니다.");
                return;
            }

            coin -= price;
            unitList[unitName] = true;

            Dictionary<string, object> updateData = new Dictionary<string, object>();
            updateData["Coin"] = coin;
            updateData["UnitList"] = JsonConvert.SerializeObject(unitList);

            reference.Child("UserInfo").Child(userKey).UpdateChildrenAsync(updateData).ContinueWith(saveTask =>
            {
                if (saveTask.IsFaulted || saveTask.IsCanceled)
                {
                    onFailure(unitName + " 구매 저장 실패");
                    return;
                }

                AdvancedUserState state = new AdvancedUserState();
                state.UserKey = userKey;
                state.NickName = AdvancedDataUtility.ReadString(snapshot.Child("NickName"));
                state.Coin = coin;
                state.Score = AdvancedDataUtility.ReadInt(snapshot.Child("Score"), 0);
                state.UnitList = unitList;
                state.Inventory = AdvancedDataUtility.ParseInventory(AdvancedDataUtility.ReadString(snapshot.Child("Inventory")));

                onSuccess(state, unitName + " 구매 완료");
            });
        });
    }
}

public class GameResultFeature
{
    readonly DatabaseReference reference;

    public GameResultFeature(DatabaseReference reference)
    {
        this.reference = reference;
    }

    public void SaveResult(string userKey, string resultName, int resultScore, int rewardCoin, Action<AdvancedUserState, string> onSuccess, Action<string> onFailure)
    {
        if (string.IsNullOrEmpty(userKey))
        {
            onFailure("로그인 정보가 없습니다.");
            return;
        }

        reference.Child("UserInfo").Child(userKey).GetValueAsync().ContinueWith(task =>
        {
            if (task.IsFaulted || task.IsCanceled || !task.Result.Exists)
            {
                onFailure("게임 결과 저장을 위한 유저 데이터 불러오기 실패");
                return;
            }

            DataSnapshot snapshot = task.Result;
            int oldCoin = AdvancedDataUtility.ReadInt(snapshot.Child("Coin"), 0);
            int oldScore = AdvancedDataUtility.ReadInt(snapshot.Child("Score"), 0);
            int newCoin = oldCoin + rewardCoin;
            int newScore = oldScore < resultScore ? resultScore : oldScore;

            Dictionary<string, object> updateData = new Dictionary<string, object>();
            updateData["Coin"] = newCoin;
            updateData["Score"] = newScore;

            reference.Child("UserInfo").Child(userKey).UpdateChildrenAsync(updateData).ContinueWith(saveTask =>
            {
                if (saveTask.IsFaulted || saveTask.IsCanceled)
                {
                    onFailure("게임 결과 저장 실패");
                    return;
                }

                AdvancedUserState state = new AdvancedUserState();
                state.UserKey = userKey;
                state.NickName = AdvancedDataUtility.ReadString(snapshot.Child("NickName"));
                state.Coin = newCoin;
                state.Score = newScore;
                state.UnitList = AdvancedDataUtility.ParseUnitList(AdvancedDataUtility.ReadString(snapshot.Child("UnitList")));
                state.Inventory = AdvancedDataUtility.ParseInventory(AdvancedDataUtility.ReadString(snapshot.Child("Inventory")));

                string message;
                if (newScore == resultScore && resultScore > oldScore)
                {
                    message = resultName + " 완료: +" + rewardCoin + " 코인, 최고 점수 " + resultScore + "점 갱신";
                }
                else
                {
                    message = resultName + " 완료: +" + rewardCoin + " 코인, 기존 최고 점수 " + oldScore + "점 유지";
                }

                onSuccess(state, message);
            });
        });
    }
}

public class AuctionFeature
{
    readonly DatabaseReference reference;

    public AuctionFeature(DatabaseReference reference)
    {
        this.reference = reference;
    }

    public void RegisterItem(string userKey, string userNickName, string itemName, int price, Action<AdvancedUserState, string> onSuccess, Action<string> onFailure)
    {
        if (string.IsNullOrEmpty(userKey))
        {
            onFailure("로그인 정보가 없습니다.");
            return;
        }

        reference.Child("UserInfo").Child(userKey).GetValueAsync().ContinueWith(task =>
        {
            if (task.IsFaulted || task.IsCanceled || !task.Result.Exists)
            {
                onFailure("판매 등록을 위한 유저 데이터 불러오기 실패");
                return;
            }

            DataSnapshot snapshot = task.Result;
            Dictionary<string, int> inventory = AdvancedDataUtility.ParseInventory(AdvancedDataUtility.ReadString(snapshot.Child("Inventory")));

            if (!inventory.ContainsKey(itemName) || inventory[itemName] <= 0)
            {
                onFailure(itemName + " 보유 개수가 부족해 판매 등록할 수 없습니다.");
                return;
            }

            inventory[itemName]--;

            string listingKey = reference.Child("AuctionList").Push().Key;
            string sellerNickName = string.IsNullOrEmpty(userNickName) ? AdvancedDataUtility.ReadString(snapshot.Child("NickName")) : userNickName;

            Dictionary<string, object> updateData = new Dictionary<string, object>();
            updateData["UserInfo/" + userKey + "/Inventory"] = JsonConvert.SerializeObject(inventory);
            updateData["AuctionList/" + listingKey + "/SellerKey"] = userKey;
            updateData["AuctionList/" + listingKey + "/SellerNickName"] = sellerNickName;
            updateData["AuctionList/" + listingKey + "/ItemName"] = itemName;
            updateData["AuctionList/" + listingKey + "/Price"] = price;
            updateData["AuctionList/" + listingKey + "/IsSold"] = false;
            updateData["AuctionList/" + listingKey + "/CreatedAt"] = ServerValue.Timestamp;

            reference.UpdateChildrenAsync(updateData).ContinueWith(saveTask =>
            {
                if (saveTask.IsFaulted || saveTask.IsCanceled)
                {
                    onFailure("판매 등록 실패");
                    return;
                }

                AdvancedUserState state = CreateStateFromSnapshot(userKey, snapshot);
                state.Inventory = inventory;
                onSuccess(state, itemName + " 판매 등록 완료");
            });
        });
    }

    public void LoadOpenItems(Action<List<AuctionItemData>> onSuccess, Action<string> onFailure)
    {
        reference.Child("AuctionList").GetValueAsync().ContinueWith(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                onFailure("거래소 목록 불러오기 실패");
                return;
            }

            List<AuctionItemData> openItems = new List<AuctionItemData>();
            foreach (DataSnapshot itemSnapshot in task.Result.Children)
            {
                if (AdvancedDataUtility.ReadBool(itemSnapshot.Child("IsSold"), false))
                {
                    continue;
                }

                AuctionItemData item = new AuctionItemData();
                item.Key = itemSnapshot.Key;
                item.SellerKey = AdvancedDataUtility.ReadString(itemSnapshot.Child("SellerKey"));
                item.SellerNickName = AdvancedDataUtility.ReadString(itemSnapshot.Child("SellerNickName"));
                item.ItemName = AdvancedDataUtility.ReadString(itemSnapshot.Child("ItemName"));
                item.Price = AdvancedDataUtility.ReadInt(itemSnapshot.Child("Price"), 0);

                if (!string.IsNullOrEmpty(item.Key) && !string.IsNullOrEmpty(item.ItemName))
                {
                    openItems.Add(item);
                }
            }

            onSuccess(openItems);
        });
    }

    public void BuyItem(string buyerKey, AuctionItemData item, Action<AdvancedUserState, string> onSuccess, Action<string> onFailure)
    {
        if (string.IsNullOrEmpty(buyerKey))
        {
            onFailure("로그인 정보가 없습니다.");
            return;
        }

        if (item.SellerKey == buyerKey)
        {
            onFailure("내가 등록한 아이템은 구매할 수 없습니다.");
            return;
        }

        reference.Child("AuctionList").Child(item.Key).GetValueAsync().ContinueWith(listingTask =>
        {
            if (listingTask.IsFaulted || listingTask.IsCanceled || !listingTask.Result.Exists)
            {
                onFailure("판매 정보를 다시 불러오지 못했습니다.");
                return;
            }

            DataSnapshot listingSnapshot = listingTask.Result;
            if (AdvancedDataUtility.ReadBool(listingSnapshot.Child("IsSold"), false))
            {
                onFailure("이미 판매 완료된 아이템입니다.");
                return;
            }

            string sellerKey = AdvancedDataUtility.ReadString(listingSnapshot.Child("SellerKey"));
            string itemName = AdvancedDataUtility.ReadString(listingSnapshot.Child("ItemName"));
            int price = AdvancedDataUtility.ReadInt(listingSnapshot.Child("Price"), 0);

            if (sellerKey == buyerKey)
            {
                onFailure("내가 등록한 아이템은 구매할 수 없습니다.");
                return;
            }

            LoadBuyerAndSellerForPurchase(buyerKey, sellerKey, item.Key, itemName, price, onSuccess, onFailure);
        });
    }

    void LoadBuyerAndSellerForPurchase(string buyerKey, string sellerKey, string listingKey, string itemName, int price, Action<AdvancedUserState, string> onSuccess, Action<string> onFailure)
    {
        reference.Child("UserInfo").Child(buyerKey).GetValueAsync().ContinueWith(buyerTask =>
        {
            if (buyerTask.IsFaulted || buyerTask.IsCanceled || !buyerTask.Result.Exists)
            {
                onFailure("구매자 데이터를 불러오지 못했습니다.");
                return;
            }

            DataSnapshot buyerSnapshot = buyerTask.Result;
            int buyerCoin = AdvancedDataUtility.ReadInt(buyerSnapshot.Child("Coin"), 0);
            Dictionary<string, int> buyerInventory = AdvancedDataUtility.ParseInventory(AdvancedDataUtility.ReadString(buyerSnapshot.Child("Inventory")));

            if (buyerCoin < price)
            {
                onFailure("거래소 아이템 구매에 필요한 코인이 부족합니다.");
                return;
            }

            reference.Child("UserInfo").Child(sellerKey).GetValueAsync().ContinueWith(sellerTask =>
            {
                if (sellerTask.IsFaulted || sellerTask.IsCanceled || !sellerTask.Result.Exists)
                {
                    onFailure("판매자 데이터를 불러오지 못했습니다.");
                    return;
                }

                int sellerCoin = AdvancedDataUtility.ReadInt(sellerTask.Result.Child("Coin"), 0);
                buyerCoin -= price;
                sellerCoin += price;
                buyerInventory[itemName] = AdvancedDataUtility.GetItemCount(buyerInventory, itemName) + 1;

                Dictionary<string, object> updateData = new Dictionary<string, object>();
                updateData["UserInfo/" + buyerKey + "/Coin"] = buyerCoin;
                updateData["UserInfo/" + buyerKey + "/Inventory"] = JsonConvert.SerializeObject(buyerInventory);
                updateData["UserInfo/" + sellerKey + "/Coin"] = sellerCoin;
                updateData["AuctionList/" + listingKey + "/IsSold"] = true;
                updateData["AuctionList/" + listingKey + "/BuyerKey"] = buyerKey;
                updateData["AuctionList/" + listingKey + "/SoldAt"] = ServerValue.Timestamp;

                reference.UpdateChildrenAsync(updateData).ContinueWith(saveTask =>
                {
                    if (saveTask.IsFaulted || saveTask.IsCanceled)
                    {
                        onFailure("거래소 구매 저장 실패");
                        return;
                    }

                    AdvancedUserState state = CreateStateFromSnapshot(buyerKey, buyerSnapshot);
                    state.Coin = buyerCoin;
                    state.Inventory = buyerInventory;
                    onSuccess(state, itemName + " 거래소 구매 완료");
                });
            });
        });
    }

    AdvancedUserState CreateStateFromSnapshot(string userKey, DataSnapshot snapshot)
    {
        AdvancedUserState state = new AdvancedUserState();
        state.UserKey = userKey;
        state.NickName = AdvancedDataUtility.ReadString(snapshot.Child("NickName"));
        state.Coin = AdvancedDataUtility.ReadInt(snapshot.Child("Coin"), 0);
        state.Score = AdvancedDataUtility.ReadInt(snapshot.Child("Score"), 0);
        state.UnitList = AdvancedDataUtility.ParseUnitList(AdvancedDataUtility.ReadString(snapshot.Child("UnitList")));
        state.Inventory = AdvancedDataUtility.ParseInventory(AdvancedDataUtility.ReadString(snapshot.Child("Inventory")));
        return state;
    }
}
