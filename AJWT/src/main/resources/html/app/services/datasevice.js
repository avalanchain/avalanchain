(function () {
    'use strict';
    var serviceId = 'dataservice';
    angular.module('avalanchain').factory(serviceId, ['$http', '$q', 'common', 'dataProvider', '$filter', '$timeout', 'websocketservice', dataservice]);

    function dataservice($http, $q, common, dataProvider, $filter, $timeout, websocketservice) {
        var logger = common.logger.getLogFn('dataservice');
        var logError = common.logger.getLogFn('dataservice', 'error');
        var logWarning = common.logger.getLogFn('dataservice', 'warn');
        var data = {};
        data.nodesLoaded = false;
        var service = {
            getData: getData,
            sendPayment: sendPayment,
            getTransactions: getTransactions,
            newAccount: newAccount,
            getAccounts: getAccounts,
            getAccs: getAccs,
            getYData: getYData,
            addCluster: addCluster,
            addNode: addNode,
            addStream: addStream,
            deleteCluster: deleteCluster,
            clearAllProcesses: clearAllProcesses,
            getQuoka: getQuoka,
            commondata: commondata,
            getId: getId,
            getChat: getChat,
            sendMessage: sendMessage,
            getNodes: getNodes,
            getUsers: getUsers,
            newUser:newUser,
            getYahoo: getYahoo,
            getLog: getLog
        };

        return service;

        function getChat(vm) {
            if(vm){
                if(!vm.id)
                    vm.id = getId();
                vm.messages = [];
                if(data.messages)
                    vm.messages = angular.copy(data.messages);
                vm.lastMessage = new Date();
                var sides = ['right', 'left'];
                vm.newMesData = function(mes) {
                    vm.messages = vm.messages || [];
                    vm.messages.push({
                        id: getId(),
                        name: mes.nodeName,
                        date: mes.dt,
                        message: mes.message,
                        side: sides[1],

                    })
                    vm.lastMessage = new Date();
                    //}
                    //data.messages = vm.messages;

                };
                //data.nodesLoaded=true;
                vm.removeListener = function() {
                    websocketservice.mlisteners.removeListener(vm);
                };
                websocketservice.mlisteners.addListener(vm);
                //vm.messages = data.messages;
            }
        }

        function sendMessage(mes) {
            var message = {msg:mes};
            return dataProvider.post({}, '/v1/chat/newMessage', message,
                function success(data, status) {
                    return data;
                },
                function fail(data, status) {
                    if (data == "Already Added") {
                        logWarning(data);
                    } else {
                        logError(data);
                    }

                });
        }


        function getAccounts(vm) {
            var accounts = [];
            if(vm){
                if(!vm.id)
                    vm.id = getId();
                vm.accountData = function(accounts) {
                    vm.accounts = vm.accounts || [];
                    for (var pr in accounts) {
                        var acc = accounts[pr];
                        var same = vm.accounts.filter(function (ac) {
                            return ac.name == acc.account.accountId
                        });
                        if(same.length == 0){
                            var property = '';
                            for (var prop in acc.status) {
                                if (acc.status.hasOwnProperty(prop)){
                                    property =  prop;
                                    break;
                                }

                            }
                            vm.accounts.push({
                                name: acc.account.accountId,
                                publicKey: acc.account.accountId,
                                balance: acc.balance,
                                status: property,
                                signed: true,
                                expired: acc.account.expire,
                                ref: {
                                    address: acc.account.accountId
                                }
                            });

                        }
                        data.accounts = vm.accounts;
                        vm.totalItems = vm.accounts.length;
                    }

                };

                vm.removeListener = function() {
                    websocketservice.alisteners.removeListener(vm);
                };
                websocketservice.alisteners.addListener(vm);
                vm.accounts = data.accounts;
            }
            // if (accounts.length < 1) {
            //     for (var i = 0; i < 200; i++) {
            //         accounts.push({
            //             name: getId(),
            //             publicKey: getId(),
            //             balance: Math.floor(Math.random() * 1000) + 100,
            //             status: Math.floor(Math.random() * 3) + 1,
            //             signed: true,
            //             ref: {
            //                 address: getId()
            //             }
            //         })
            //     }
            // }
            //
            // return accounts;
            // return dataProvider.get(sc, '/api/account/all', function (data, status) {
            //     //$scope.GetAllProgresses = data;
            // });
        }

        function getUsers() {
            var sc = {};
            return dataProvider.get(sc, '/v1/users', {},
                function success(data, status) {
                    logger('GET ' + data.length + ' USERS');
                    return data;
                },
                function fail(data, status) {
                    if (data == "Already Added") {
                        logWarning(data);
                    } else {
                        logError(data);
                    }

                });
        }

        function getAccs() {
            var sc = {};
            return dataProvider.get(sc, '/v1/currency/accounts', {},
                function success(data, status) {
                    // logger('GET ' + data.length + ' USERS');
                    return data;
                },
                function fail(data, status) {
                    if (data == "Already Added") {
                        logWarning(data);
                    } else {
                        logError(data);
                    }

                });
        }

        function sendPayment(payment) {
            return $http.post('/api/transaction/submit', payment)
                .success(function (data, status, headers, config) {
                    logger("Transaction submited!"); //'" + JSON.stringify(data) + "'
                    return data;
                })
                .error(function (data, status, headers, config) {
                    var err = status + ", " + data;
                    logError("Request failed: " + err);
                    //$scope.result = "Request failed: " + err;
                    return "error";
                });
        }

        function getTransactions(vm) {
            if(vm){
                if(!vm.id)
                    vm.id = getId();
                vm.transactionData = function(transaction) {
                    vm.transactions = vm.transactions || [];
                    // var same = vm.nodes.filter(function (nd) {
                    //     return nd.data.address.port == info.NodeUp.address.port
                    // });
                    // if(same.length == 0){
                        //var num = vm.nodes.length + 1;
                        vm.transactions.push({
                            from: transaction.from.replace(/-/gi, ''),
                            to: transaction.to.replace(/-/gi, ''),
                            amount: transaction.amount,
                            pub: transaction.pub.X
                        });
                    // }
                    data.transactions = vm.transactions;
                    // data.nodesLoaded=true;
                };
                vm.removeListener = function() {
                    websocketservice.tlisteners.removeListener(vm);
                };
                websocketservice.tlisteners.addListener(vm);
                vm.transactions = data.transactions;
            }
        }

        function getLog() {
            var sc = {};
            var tusers = ["EUR/USD", "USD/EUR", "USD/JPY", "USD/GBP", "USD/AUD", "USD/CHF", "USD/SEK", "USD/NOK", "USD/RUB"];
            var tsystems = ["Error", "Warning", "Success"];
            var ausers = ["send", "receive", "denied", "accept"];
            var asystems = ["signed", "new cluster", "new node", "new account"];
            var types = ["users", "system"]
            var transactions = [];
            var account = '';
            for (var i = 1; i <= 1000; i++) {
                var type = (i % 2) == 0 ? types[0] : types[1];
                var node = Math.floor(Math.random() * data.nodes.length);
                var action, value, typename = '';
                if (type === "users") {
                    account = getId();
                    action = ausers[Math.floor(Math.random() * ausers.length)];
                    typename = tusers[Math.floor(Math.random() * tusers.length)];
                } else {
                    account = 'system';
                    action = asystems[Math.floor(Math.random() * asystems.length)];
                    typename = tsystems[Math.floor(Math.random() * tsystems.length)];
                }
                transactions.push({
                    id: getId(),
                    publicKey: getId(),
                    node: data.nodes[node].id,
                    action: action,
                    account: account,
                    type: type,
                    typename: typename,
                    date: new Date()
                })
            }
            return transactions;
        }

        function getStreams() {
            var typenames = ["EUR/USD", "USD/EUR", "USD/JPY", "USD/GBP", "USD/AUD", "USD/CHF", "USD/SEK", "USD/NOK", "USD/RUB"];
            var types = ["user", "transaction"]
            var streams = [];
            for (var i = 1; i <= 200; i++) {
                var type = (i % 2) == 0 ? types[0] : types[1];
                var typename = Math.floor(Math.random() * typenames.length);
                if(data.nodes.length>0){
                    var node = Math.floor(Math.random() * data.nodes.length);
                    streams.push({
                        id: getId(),
                        //publicKey: data.nodes[node].publicKey,
                        node: data.nodes[node].id,
                        type: type,
                        typename: typenames[typename],
                        date: new Date()
                    })
                }

            }
            return streams
        }

        function getNodes(vm) {
            if(vm){
                if(!vm.id)
                    vm.id = getId();
                vm.nodeData = function(info) {
                    if(!info.NodeUp){
                        return;
                    }
                    vm.nodes = vm.nodes || [];
                    var same = vm.nodes.filter(function (nd) {
                        return nd.data.address.port == info.NodeUp.address.port
                    });
                    if(same.length == 0){
                        var num = vm.nodes.length + 1;
                        vm.nodes.push({
                            id: getId(),
                            data: info.NodeUp,
                            name: 'ND-' + num,
                            publicKey: getId(),
                            cluster: data.clusters[0].id
                        });
                    }
                    data.nodes = vm.nodes;
                    data.nodesLoaded=true;
                };
                vm.removeListener = function() {
                    websocketservice.nlisteners.removeListener(vm);
                };
                websocketservice.nlisteners.addListener(vm);
                vm.nodes = data.nodes;
            }
            //
            // var nodes = [];
            // return nodes;
        }

        function getClusters() {
            var clusters = [];
            for (var i = 1; i <= 1; i++) {
                clusters.push({
                    id: getId(),
                    name: 'CL-' + i,
                    publicKey: getId(),
                })
            }
            return clusters;
        }


        function getYData1() {
            var url = "https://query.yahooapis.com/v1/public/yql";
            //var symbol = '"EURUSD","USDEUR", "USDJPY", "USDGBP", "USDAUD", "USDCHF", "USDSEK", "USDNOK", "USDRUB", "USDTRY", "USDBRL", "USDCAD", "USDCNY", "USDHKD", "USDINR", "USDKRW", "USDMXN", "USDNZD", "USDSGD", "USDZAR"';
            var symbol = '"EURUSD","USDEUR", "USDJPY", "USDGBP"';
            var data = encodeURIComponent("select * from yahoo.finance.xchange where pair in (symbol)");
            data = data.replace("symbol", symbol);
            /*
             http://query.yahooapis.com/v1/public/yql?q=select%20*%20from%20yahoo.finance.quotes%20where%20symbol%20in%20('aapl')&format=json&diagnostics=true&env=http://datatables.org/alltables.env
             */
            var str1 = url.concat("?q=", data);
            str1 = str1.concat("&format=json&env=store://datatables.org/alltableswithkeys"); //http://datatables.org/alltables.env

            var sc = {};


            return dataProvider.get(sc, str1, function(data, status) {
                //$scope.GetAllProgresses = data;
            });
            //"AUD", "CHF", "SEK", "NOK", "RUB", "TRY", "BRL", "CAD", "CNY", "HKD", "INR", "KRW", "MXN", "NZD", "SGD", "ZAR"
        }
        function getYData() {
            var defer = $q.defer();
            var rates = {
                "EUR": 1/(1.1795),
                "GBP": 0.88208,
                "JPY": 132.66,
                "USD": 1.1795,
                "AUD": 1.4373,
                "BRL": 3.3784,
                "CAD": 1.3894,
                "CHF": 1.0707,
                "CNY": 7.2382,
                "HKD": 8.0948,
                "INR": 71.039,
                "KRW": 1251.9,
                "MXN": 22.071,
                "NOK": 8.9905,
                "NZD": 1.5024,
                "RUB": 63.408,
                "SEK": 9.5238,
                "SGD": 1.5047,
                "TRY": 3.7387,
                "ZAR": 14.241
            };
            var dt = [];

            for (var name in rates) {
                if (rates.hasOwnProperty(name)) {
                    var erate = rates[name] * 0.01 * Math.random();
                    var main = name === 'EUR' ? 'USD' : 'EUR';
                    dt.push({
                        Ask: (rates[name] + erate).toFixed(4),
                        Bid: (rates[name] - erate).toFixed(4),
                        Rate: (rates[name] + erate).toFixed(4),
                        Name: main + '/' + name,
                        Time: getTime(new Date()),
                        Date: new Date()
                    });
                }
            }
            //for (var i = 1; i <= rates.length; i++) {
            //    var erate =  rates['USD'] * 0.01 * Math.random();
            //    dt.push({
            //        Ask: (rates['USD'] + erate).toFixed(4),
            //        Bid: (rates['USD'] - erate).toFixed(4),
            //        Rate: (rates['USD'] + erate).toFixed(4),
            //        Name: 'EUR' + '/' + 'USD',
            //        Time: getTime(new Date()),
            //        Date: new Date()
            //});
            //     var grate = rates['GBP'] * 0.01 * Math.random();
            //    dt.push({
            //        Ask: (rates['GBP'] + grate).toFixed(4),
            //        Bid: (rates['GBP'] - grate).toFixed(4),
            //        Rate: (rates['GBP'] + grate).toFixed(4),
            //        Name: 'EUR' + '/' + 'GBP',
            //        Time: getTime(new Date()),
            //        Date: new Date()
            //    });
            //    var jrate = rates['JPY'] * 0.01 * Math.random();
            //    dt.push({
            //        Ask: (rates['JPY'] + jrate).toFixed(4),
            //        Bid: (rates['JPY'] - jrate).toFixed(4),
            //        Rate: (rates['JPY'] + jrate).toFixed(4),
            //        Name: 'EUR' + '/' + 'JPY',
            //        Time: getTime(new Date()),
            //        Date: new Date()
            //    });
            //    var urate = rates['EUR'] * 0.01 * Math.random();
            //    dt.push({
            //        Ask: (rates['EUR'] + urate).toFixed(4),
            //        Bid: (rates['EUR'] - urate).toFixed(4),
            //        Rate: (rates['EUR'] + urate).toFixed(4),
            //        Name: 'USD' + '/' + 'EUR',
            //        Time: getTime(new Date()),
            //        Date: new Date()
            //    });
            // };

            $timeout(function() {
                    defer.resolve(dt);
                },
                200);

            return defer.promise;
        }
        function getPrices(currency) {
            if (!currency)
                currency = 'USD';

            var url = "https://min-api.cryptocompare.com/data/price?fsym=" + currency + "&tsyms=BTC,ETH,EUR,LTC";

            var sc = {};


            return dataProvider.get(sc, url, function(data, status) {
                //$scope.GetAllProgresses = data;
            });

        }

        function getTime(d) {
            var seconds = d.getSeconds() < 10 ? '0' + d.getSeconds() : d.getSeconds();
            return d.getHours() + ":" + d.getMinutes() + ":" + seconds;
        }

        function msToTime(s) {

            // Pad to 2 or 3 digits, default is 2
            function pad(n, z) {
                z = z || 2;
                return ('00' + n).slice(-z);
            }

            var ms = s % 1000;
            s = (s - ms) / 1000;
            var secs = s % 60;
            s = (s - secs) / 60;
            var mins = s % 60;
            var hrs = (s - mins) / 60;

            return pad(hrs) + ':' + pad(mins) + ':' + pad(secs) + '.' + pad(ms, 3);
        }


        function mapping(data, $scope){
            for (var i = 0; i < data.length; i++) {
                var from = mapvalues(data[i].Name.split('/')[1]);
                var to = mapvalues(data[i].Name.split('/')[0]);
                data[i].Name = from + '/' + to;
                data[i].id = from +  to;
                if($scope.prices && from!='USD'){
                    var delta = (data[i].Ask - data[i].Bid).toFixed(7);
                    data[i].Ask = Number(1/$scope.prices[from]) + Number(delta);
                    data[i].Bid = Number(1/$scope.prices[from]) - Number(delta);
                    data[i].Ask = (data[i].Ask).toFixed(2);
                    data[i].Bid = (data[i].Bid).toFixed(2);
                }else{
                    var delta = ((data[i].Ask - data[i].Bid)/100).toFixed(7);
                    data[i].Ask = Number($scope.prices[to]) + Number(delta);
                    data[i].Bid = Number($scope.prices[to]) - Number(delta);
                    data[i].Ask = (data[i].Ask).toFixed(7);
                    data[i].Bid = (data[i].Bid).toFixed(7);
                }

            }
            return data;
        }

        function mapvalues(value){
            var val = value;
            // str = str.replace(/abc/g, '');
            switch (value) {
                case 'JPY':
                    val = 'LTC';
                    break;
                case "EUR":
                    val = 'BTC';
                    break;
                case 'GBP':
                    val = 'ETH';
                    break;
                // default:
                //   alert( 'Я таких значений не знаю' );
            }
            return val;
        }

        function addCluster() {

        }

        function addNode() {
            var sc = {};
            return dataProvider.post(sc, '/v1/nodes/newNode', {},
                function success(data, status) {
                    //return data;
                },
                function fail(data, status) {
                    if (data == "Already Added") {
                        logWarning(data);
                    } else {
                        logError(data);
                    }

                });
        }

        function addStream() {

        }

        function deleteCluster() {

        }

        function clearAllProcesses() {

        }

        function newAccount() {//
            return dataProvider.post({}, '/v1/currency/newAccount1000', {},
                function success(data, status) {
                    logger("Account created!");
                    //return data;
                },
                function fail(data, status) {
                    if (data == "Already Added") {
                        logWarning(data);
                    } else {
                        //logError(data);
                    }

                });
        }

        function newUser() {
            var defer = $q.defer();
            var status = 200;

            $timeout(function () {
                defer.resolve(status);
            }, 200)
            logger("User created!");
            return defer.promise;
        }

        function getQuoka(datayahoo) {
            var data = datayahoo;
            var quoka = 0;
            if (data) {
                for (var i = 0; i < commondata().currencies().length; i++) {

                    if (i === 0) quoka += 0.45;
                    else {
                        quoka += (1 / data[i].Rate) * commondata().percentage()[i];
                    }

                }
                quoka = Math.round(quoka * 1000) / 1000;
            }
            return quoka;
        }

        function commondata() {
            var curr = ["USD", "EUR", "JPY", "GBP", "AUD", "CHF", "SEK", "NOK", "RUB", "TRY", "BRL", "CAD", "CNY", "HKD", "INR", "KRW", "MXN", "NZD", "SGD", "ZAR"];
            var percentage = [45, 17, 12, 6, 4, 3, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0.5, 0.5];

            return {
                currencies: function () {
                    return curr;
                },
                percentage: function () {
                    return percentage;
                }
            };
        }

        function getYahoo(vm) {
            if(!vm.id)
                vm.id = getId();
            vm.newData = function(info) {
                vm.yahoodata = vm.yahoodata || [];
                if (vm.yahoodata.length > 16) {
                    vm.yahoodata.pop();
                }
                vm.yahoodata.unshift(info);
            };
            //vm.nodes = data.yahoo;
            websocketservice.ylisteners.addListener(vm);
            vm.removeListener = function() {
                websocketservice.ylisteners.removeListener(vm);
            };
        }

        //  var data = {};

        function getData() {
            var defer = $q.defer();
            data.clusters = data.clusters ? data.clusters : getClusters();
            data.nodes = data.nodes || getNodes(data);
            data.accounts = data.accounts ? data.accounts : getAccs();//getAccounts(data);
            data.transactions = data.transactions || getTransactions(data);
            data.messages = data.messages ? data.messages : getChat(data);
            data.yahoodata = data.yahoodata ? data.yahoodata : getYahoo(data);


            if(data.nodesLoaded){
                data.streams = data.streams.length !== 0 ? data.streams : getStreams();
                data.log = data.log ? data.log : getLog();
            }
            else{
                data.streams = [];
                data.transactions = [];
            }
            $timeout(function () {
                defer.resolve(data);
            }, 200)
            // var yh = websocetservice.collection;//getYahoo();
            return defer.promise;
        }

        function getId() {
            return common.createGuid().replace(/-/gi, '')
        }
    }
})();
