(function () {
    'use strict';
    var controllerId = 'dashboard_investor';
    angular.module('avalanchain').controller(controllerId, ['common', 'dataservice', '$scope', '$uibModal', '$rootScope', dashboard_investor]);

    function dashboard_investor(common, dataservice, $scope, $uibModal, $rootScope) {
        var getLogFn = common.logger.getLogFn;
        var log = getLogFn(controllerId);
        var vm = this;
        vm.info = 'Dashboard Investor';

        vm.spinOption = {
            min: 0,
            max: 10000000000,
            step: 1,
            decimals: 0,
            boostat: 5,
            maxboostedstep: 100000,
        };
        var currencies = dataservice.commondata().currencies();
        var percentage = dataservice.commondata().percentage();
        $scope.currlist = [];
        $scope.quoka = 0;
        for (var i = 0; i < currencies.length; i++) {
            var color = '#ed5565';
            if (i > 0) color = getRandomColor();
            $scope.currlist.push({
                id: currencies[i],
                value: percentage[i],
                color: color,

            });
        }
        vm.amount = 100;
        vm.periods = []

        for (var i = 1; i < 11; i++) {
            var per = {
                percent: 27.5 - i * 2.5,
                currency: 1000 * i,
                complete: i < 6
            }

            vm.periods.push(per);
        }

        $scope.currencies = [{
            id: 0,
            currency: 'USD',
            name: 'Dollar'
        }, {
            id: 1,
            currency: 'BTC',
            name: 'Bitcoin'
        }, {
            id: 2,
            currency: 'ETH',
            name: 'Ethereum'
        }];
        $scope.currency = $scope.currencies[0];

        //vm.datayahoo = dataservice.getYData();
        function getRandomColor() {
            var letters = '0123456789ABCDEF'.split('');
            var color = '#';
            for (var i = 0; i < 6; i++) {
                color += letters[Math.floor(Math.random() * 16)];
            }
            return color;
        }

        setInterval(function updateRandom() {
            getMessageCount();
        }, 3000);

        var lineAreaOptions = {
            series: {
                lines: {
                    show: true,
                    lineWidth: 2,
                    fill: true,
                    fillColor: {
                        colors: [{
                                opacity: 0.7
                            },
                            {
                                opacity: 0.5
                            }
                        ]
                    }
                }
            },
            xaxis: {
                tickDecimals: 0
            },
            yaxis: {
                min: 37.40
            },
            //yaxis: { min: 37.27, ticks: [37.275, 37.300, 37.325, 37.350, 37.375, 37.400, 37.425, 37.450, 37.475, 37.500, 37.525] },
            colors: ["#ed5565"],
            grid: {
                color: "#999999",
                hoverable: true,
                clickable: true,
                tickColor: "#D4D4D4",
                borderWidth: 0
            },
            legend: {
                show: false
            },
            tooltip: true,
            tooltipOpts: {
                content: "x: %x, y: %y"
            }
        };
        var dataF = [
            [1, 37.475],
            [2, 37.48],
            [3, 37.495],
            [4, 37.47],
            [5, 37.485],
            [6, 37.47],
            [7, 37.495],
            [8, 37.47],
            [9, 37.485],
            [10, 37.475],
            [11, 37.49],
            [12, 37.495],
            [13, 37.49]
        ];
        var lineAreaData = [{
            label: "line",
            data: dataF
        }];
        $scope.flotLineAreaOptions = lineAreaOptions;
        $scope.flotLineAreaData = lineAreaData;
        var container = $("#flot-line-chart-moving");
        var maximum = container.outerWidth() / 2 || 300;

        function getRandomData() {

            if (dataF.length) {
                dataF = dataF.slice(1);
            }

            while (dataF.length < maximum) {
                var previous = dataF.length ? dataF[dataF.length - 1] : 38;
                var y = previous + Math.random() * 10 - 5;
                dataF.push(y < 37 ? 37.1 : y > 38 ? 38 : y);
            }

            // zip the generated y values with the x values

            var res = [];
            for (var i = 0; i < dataF.length; ++i) {
                res.push([i, dataF[i]]);
            }

            return res;
        }
        //unused
        $scope.payment = {
            fromAcc: {},
            toAcc: {}
        };

        $scope.openModal = function (currency, action) {
            //var m = new Mnemonic(96);
            $rootScope.modal = {
                payment: $scope.payment,
                currency: currency,
                action: action,
                balance: 300,
                quoka: $scope.quoka,
                amount: 10,
                from: currency.Name.split('/')[0],
                to: currency.Name.split('/')[1],
            }

            var modalInstance = $uibModal.open({
                templateUrl: '/Content/views/dashboard/quoka_payment.html',
                controller: modalCtrl
            });
        };


        var data = {
            "US": 298,
            "SA": 200,
            "DE": 220,
            "FR": 540,
            "CN": 120,
            "AU": 760,
            "BR": 550,
            "IN": 200,
            "GB": 120
        };

        vm.data = data;

        var data1 = [
            [gd(2012, 1, 1), 7],
            [gd(2012, 1, 2), 6],
            [gd(2012, 1, 3), 4],
            [gd(2012, 1, 4), 8],
            [gd(2012, 1, 5), 9],
            [gd(2012, 1, 6), 7],
            [gd(2012, 1, 7), 5],
            [gd(2012, 1, 8), 4],
            [gd(2012, 1, 9), 7],
            [gd(2012, 1, 10), 8],
            [gd(2012, 1, 11), 9],
            [gd(2012, 1, 12), 6],
            [gd(2012, 1, 13), 4],
            [gd(2012, 1, 14), 5],
            [gd(2012, 1, 15), 11],
            [gd(2012, 1, 16), 8],
            [gd(2012, 1, 17), 8],
            [gd(2012, 1, 18), 11],
            [gd(2012, 1, 19), 11],
            [gd(2012, 1, 20), 6],
            [gd(2012, 1, 21), 6],
            [gd(2012, 1, 22), 8],
            [gd(2012, 1, 23), 11],
            [gd(2012, 1, 24), 13],
            [gd(2012, 1, 25), 7],
            [gd(2012, 1, 26), 9],
            [gd(2012, 1, 27), 9],
            [gd(2012, 1, 28), 8],
            [gd(2012, 1, 29), 5],
            [gd(2012, 1, 30), 8],
            [gd(2012, 1, 31), 25]
        ];

        var data2 = [
            [gd(2012, 1, 1), 800],
            [gd(2012, 1, 2), 500],
            [gd(2012, 1, 3), 600],
            [gd(2012, 1, 4), 700],
            [gd(2012, 1, 5), 500],
            [gd(2012, 1, 6), 456],
            [gd(2012, 1, 7), 800],
            [gd(2012, 1, 8), 589],
            [gd(2012, 1, 9), 467],
            [gd(2012, 1, 10), 876],
            [gd(2012, 1, 11), 689],
            [gd(2012, 1, 12), 700],
            [gd(2012, 1, 13), 500],
            [gd(2012, 1, 14), 600],
            [gd(2012, 1, 15), 700],
            [gd(2012, 1, 16), 786],
            [gd(2012, 1, 17), 345],
            [gd(2012, 1, 18), 888],
            [gd(2012, 1, 19), 888],
            [gd(2012, 1, 20), 888],
            [gd(2012, 1, 21), 987],
            [gd(2012, 1, 22), 444],
            [gd(2012, 1, 23), 999],
            [gd(2012, 1, 24), 567],
            [gd(2012, 1, 25), 786],
            [gd(2012, 1, 26), 666],
            [gd(2012, 1, 27), 888],
            [gd(2012, 1, 28), 900],
            [gd(2012, 1, 29), 178],
            [gd(2012, 1, 30), 555],
            [gd(2012, 1, 31), 993]
        ];


        var dataset = [{
                label: "Number of orders",
                grow: {
                    stepMode: "linear"
                },
                data: data2,
                color: "#1ab394",
                bars: {
                    show: true,
                    align: "center",
                    barWidth: 24 * 60 * 60 * 600,
                    lineWidth: 0
                }

            },
            {
                label: "Payments",
                grow: {
                    stepMode: "linear"
                },
                data: data1,
                yaxis: 2,
                color: "#1C84C6",
                lines: {
                    lineWidth: 1,
                    show: true,
                    fill: true,
                    fillColor: {
                        colors: [{
                                opacity: 0.2
                            },
                            {
                                opacity: 0.2
                            }
                        ]
                    }
                }
            }
        ];


        var options = {
            grid: {
                hoverable: true,
                clickable: true,
                tickColor: "#d5d5d5",
                borderWidth: 0,
                color: '#d5d5d5'
            },
            colors: ["#1ab394", "#464f88"],
            tooltip: true,
            xaxis: {
                mode: "time",
                tickSize: [3, "day"],
                tickLength: 0,
                axisLabel: "Date",
                axisLabelUseCanvas: true,
                axisLabelFontSizePixels: 12,
                axisLabelFontFamily: 'Arial',
                axisLabelPadding: 10,
                color: "#d5d5d5"
            },
            yaxes: [{
                    position: "left",
                    max: 1070,
                    color: "#d5d5d5",
                    axisLabelUseCanvas: true,
                    axisLabelFontSizePixels: 12,
                    axisLabelFontFamily: 'Arial',
                    axisLabelPadding: 3
                },
                {
                    position: "right",
                    color: "#d5d5d5",
                    axisLabelUseCanvas: true,
                    axisLabelFontSizePixels: 12,
                    axisLabelFontFamily: ' Arial',
                    axisLabelPadding: 67
                }
            ],
            legend: {
                noColumns: 1,
                labelBoxBorderColor: "#d5d5d5",
                position: "nw"
            }

        };

        function gd(year, month, day) {
            return new Date(year, month - 1, day).getTime();
        }

        /**
         * Definition of variables
         * Flot chart
         */
        vm.flotData = dataset;
        vm.flotOptions = options;
        $scope.datayahoo = [];


        activate();

        function activate() {
            common.activateController([getMessageCount(), timeline()], controllerId)
                .then(function () {
                    log('Activated Dashboard Investor')
                }); //log('Activated Admin View');
        }

        $scope.$watch('yourItems', function (newVal, oldVal) {
            if (newVal !== oldVal) {
                // render charts
            }
        });
        var dataprev = [];
        //function getQuoka() {
        //    var data = $scope.datayahoo;
        //    $scope.quoka = 0;
        //    if (data) {
        //        for (var i = 0; i < currencies.length; i++) {

        //            if (i == 0) $scope.quoka += 0.45;
        //            else {
        //                $scope.quoka += (1 / data[i].Rate) * percentage[i];
        //            }

        //        }
        //        $scope.quoka = Math.round($scope.quoka * 1000) / 1000;
        //    }
        //}

        function addStatus(data) {
            if (data) {
                if (dataprev.length === 0) dataprev = data;
                for (var i = 0; i < currencies.length; i++) {

                    if(data[i]){
                        if (dataprev[i].Rate > data[i].Rate) {
                            data[i]["status"] = 'text-danger';
                            data[i]["navigation"] = 'fa fa-play fa-rotate-90';
                        } else if (dataprev[i].Rate < data[i].Rate) {
                            data[i]["status"] = 'text-navy';
                            data[i]["navigation"] = 'fa fa-play fa-rotate-270';
                        } else {
                            data[i]["status"] = '';
                            data[i]["navigation"] = 'fa fa-pause';
                        }
                    }else{
                        // console.log(dataprev[i])
                        // console.log(i)
                    }
                   
                }

                dataprev = data;
            }
            return data;
        }

        function getData() {

            if (dataF.length) {
                dataF = dataF.slice(1);
            }

            //while (dataF.length < maximum) {
            //    var previous = dataF.length ? dataF[dataF.length - 1] : 38;
            //    var y = previous + Math.random() * 10 - 5;
            //    dataF.push(y < 37 ? 37.1 : y > 38 ? 38 : y);
            //}

            // zip the generated y values with the x values
            dataF.push([dataF.length, $scope.quoka]);
            var res = [];
            for (var i = 0; i < dataF.length; ++i) {
                res.push([i, dataF[i][1]]);
            }
            //dataF = res;
            return res;
        }

        function getMessageCount() {
            // return dataservice.getYData().then(function (data) {
            //     $scope.datayahoo = addStatus(data.data.query.results.rate);
            //     $scope.quoka = dataservice.getQuoka($scope.datayahoo);
            //     $scope.flotLineAreaData[0].data = getData();
            // });
        }
    };

    function timeline() {
        // var completes = document.querySelectorAll(".complete");
        // var toggleButton = document.getElementById("toggleButton");


        // function toggleComplete() {
        //     var lastComplete = completes[completes.length - 1];
        //     lastComplete.classList.toggle('complete');
        // }

        // toggleButton.onclick = toggleComplete;


    }

})();